using System.Net;
using System.Net.Mail;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace GiftOfTheGivers.Services;

public sealed class EmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseTls { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Relief support portal";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && MailAddress.TryCreate(FromAddress, out _) && Port is > 0 and <= 65535;
}
public interface INotificationEmailSender { Task SendAsync(Notification message, CancellationToken ct); }
public sealed class SmtpNotificationEmailSender(IOptions<EmailOptions> options) : INotificationEmailSender
{
    public async Task SendAsync(Notification notification, CancellationToken ct)
    {
        var config = options.Value;
        if (!config.Enabled || !config.IsConfigured) throw new InvalidOperationException("SMTP is disabled or incomplete.");
        using var client = new SmtpClient(config.Host, config.Port) { EnableSsl = config.UseTls, UseDefaultCredentials = false };
        if (!string.IsNullOrWhiteSpace(config.Username)) client.Credentials = new NetworkCredential(config.Username, config.Password);
        using var message = new MailMessage { From = new MailAddress(config.FromAddress, config.FromName), Subject = notification.Subject, Body = notification.Body, IsBodyHtml = false };
        message.To.Add(notification.Email);
        await client.SendMailAsync(message, ct);
    }
}
public sealed class EmailNotificationWorker(IServiceScopeFactory scopes, IOptions<EmailOptions> options, ILogger<EmailNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.Value.Enabled && options.Value.IsConfigured)
            {
                try { await DeliverBatchAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogError(ex, "Notification delivery batch failed; queued messages are retained"); }
            }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) return; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
    public async Task DeliverBatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationEmailSender>();
        var now = DateTime.UtcNow;
        var pending = await db.Notifications.AsNoTracking().Where(n => n.SentAtUtc == null && n.DeliveryAttempts < 5 && (n.NextAttemptAtUtc == null || n.NextAttemptAtUtc <= now))
            .OrderBy(n => n.CreatedAtUtc).Take(20).ToListAsync(ct);
        foreach (var item in pending)
        {
            ct.ThrowIfCancellationRequested();
            // Atomically claim the message. A second worker cannot send the same attempt.
            var claimed = await db.Notifications.Where(n => n.Id == item.Id && n.SentAtUtc == null && n.DeliveryAttempts == item.DeliveryAttempts && (n.NextAttemptAtUtc == null || n.NextAttemptAtUtc <= now))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.DeliveryAttempts, n => n.DeliveryAttempts + 1).SetProperty(n => n.NextAttemptAtUtc, now.AddMinutes(5)), ct);
            if (claimed == 0) continue;
            try
            {
                await sender.SendAsync(item, ct);
                await db.Notifications.Where(n => n.Id == item.Id).ExecuteUpdateAsync(s => s.SetProperty(n => n.SentAtUtc, DateTime.UtcNow).SetProperty(n => n.LastDeliveryError, (string?)null), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Email delivery failed for notification {NotificationId}", item.Id);
                await db.Notifications.Where(n => n.Id == item.Id).ExecuteUpdateAsync(s => s.SetProperty(n => n.LastDeliveryError, "Delivery failed. Check SMTP settings and retry.")
                    .SetProperty(n => n.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(Math.Pow(2, item.DeliveryAttempts + 1))), ct);
            }
        }
    }
}
