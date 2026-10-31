using System.ComponentModel.DataAnnotations;
namespace GiftOfTheGivers.Helpers.Models;

public class Notification
{
    public int Id { get; set; }
    [StringLength(450)] public string? UserId { get; set; }
    [Required, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(200)] public string Subject { get; set; } = "";
    [Required, StringLength(2000)] public string Body { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int DeliveryAttempts { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    [StringLength(200)] public string? LastDeliveryError { get; set; }
}
