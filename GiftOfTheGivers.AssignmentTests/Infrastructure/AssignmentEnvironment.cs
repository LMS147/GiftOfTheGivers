using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace GiftOfTheGivers.AssignmentTests.Infrastructure;

[CollectionDefinition("Assignment system", DisableParallelization = true)]
public sealed class AssignmentCollection : ICollectionFixture<AssignmentEnvironment> { }

public sealed class AssignmentEnvironment : IAsyncLifetime
{
    public const string EmployeeEmail = "employee@example.test";
    public const string EmployeePassword = "Assignment!Employee2026"; // Disposable test database only.
    private readonly List<Process> processes = new();
    private readonly List<Task> logTasks = new();
    private readonly string temp = Path.Combine(Path.GetTempPath(), "gotg-assignment-" + Guid.NewGuid().ToString("N"));
    private string masterConnection = "";
    private string databaseName = "";
    private bool ownsDatabase;
    public string ConnectionString { get; private set; } = "";
    public string WebUrl { get; private set; } = "";
    public string FunctionUrl { get; private set; } = "";
    public int ProjectId { get; private set; }
    public string EvidenceDirectory { get; private set; } = "";
    public string Root { get; private set; } = "";

    public async Task InitializeAsync()
    {
        Root = FindRoot();
        EvidenceDirectory = Environment.GetEnvironmentVariable("GOTG_EVIDENCE_DIR")
            ?? Path.Combine(Root, "TestResults", "Assignment", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(temp); Directory.CreateDirectory(EvidenceDirectory);
        try
        {
            var configured = Environment.GetEnvironmentVariable("GOTG_TEST_SQL");
            if (string.IsNullOrWhiteSpace(configured))
            {
                if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Set GOTG_TEST_SQL to a local disposable SQL Server connection. See Testing/Assignment-A.md.");
                configured = "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;Encrypt=False";
            }
            var sql = new SqlConnectionStringBuilder(configured);
            var server = sql.DataSource.Split(',')[0];
            if (!(server.Equals("localhost", StringComparison.OrdinalIgnoreCase) || server == "127.0.0.1" || server == "." || server.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Assignment tests accept local SQL Server only. Never point these tests at your deployed Azure database.");
            sql.InitialCatalog = "master"; masterConnection = sql.ConnectionString;
            databaseName = "GiftOfTheGiversTest_" + Guid.NewGuid().ToString("N");
            await using (var connection = new SqlConnection(masterConnection))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand(); command.CommandText = $"CREATE DATABASE [{databaseName}]";
                await command.ExecuteNonQueryAsync(); ownsDatabase = true;
            }
            sql.InitialCatalog = databaseName; ConnectionString = sql.ConnectionString;
            await using (var db = Database())
            {
                await db.Database.MigrateAsync();
                var project = new ReliefProject { Name = "Assignment Relief Project", Location = "Test environment", Category = "Food", TargetAmount = 10000, Description = "Isolated automated-test project." };
                db.ReliefProjects.Add(project); await db.SaveChangesAsync(); ProjectId = project.Id;
            }

            var storage = Environment.GetEnvironmentVariable("GOTG_TEST_STORAGE") ?? "UseDevelopmentStorage=true";
            if (storage == "UseDevelopmentStorage=true" && !await PortOpenAsync(10000))
            {
                Start("azurite", new[] { "--silent", "--skipApiVersionCheck", "--location", Path.Combine(temp, "azurite") }, temp, new(), "azurite.log", "GOTG_AZURITE_PATH");
                await WaitForPortAsync(10000);
            }
            var webPublish = Environment.GetEnvironmentVariable("GOTG_WEB_PUBLISH_DIR") ?? Path.Combine(temp, "web");
            var functionPublish = Environment.GetEnvironmentVariable("GOTG_FUNCTIONS_PUBLISH_DIR") ?? Path.Combine(temp, "functions");
            if (!File.Exists(Path.Combine(webPublish, "GiftOfTheGivers.dll"))) await PublishAsync("GiftOfTheGivers.csproj", webPublish);
            if (!File.Exists(Path.Combine(functionPublish, "GiftOfTheGivers.Functions.dll"))) await PublishAsync("GiftOfTheGivers.Functions/GiftOfTheGivers.Functions.csproj", functionPublish);
            FunctionUrl = $"http://127.0.0.1:{FreePort()}"; WebUrl = $"http://127.0.0.1:{FreePort()}";
            var common = new Dictionary<string, string>
            {
                ["ConnectionStrings__DefaultConnection"] = ConnectionString,
                ["ASPNETCORE_ENVIRONMENT"] = "Production", ["DOTNET_ENVIRONMENT"] = "Production",
                ["Email__Enabled"] = "false", ["SeedDemoData"] = "false",
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = ""
            };
            var functionEnvironment = new Dictionary<string, string>(common)
            {
                ["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated", ["AZURE_FUNCTIONS_ENVIRONMENT"] = "Development",
                ["AzureWebJobsStorage"] = storage, ["AzureWebJobsSecretStorageType"] = "files"
            };
            Start("func", new[] { "start", "--no-build", "--port", new Uri(FunctionUrl).Port.ToString(), "--script-root", functionPublish }, functionPublish, functionEnvironment, "functions.log", "GOTG_FUNC_PATH");
            var webEnvironment = new Dictionary<string, string>(common)
            {
                ["AzureFunctions__BaseUrl"] = FunctionUrl,
                ["AzureFunctions__TaxCertificateUrl"] = FunctionUrl + "/api/tax-certificate",
                ["AzureFunctions__FunctionKey"] = "",
                ["StaffBootstrap__Enabled"] = "true", ["StaffBootstrap__Email"] = EmployeeEmail,
                ["StaffBootstrap__Password"] = EmployeePassword, ["StaffBootstrap__ResetPassword"] = "false"
            };
            Start("dotnet", new[] { Path.Combine(webPublish, "GiftOfTheGivers.dll"), "--urls", WebUrl }, webPublish, webEnvironment, "web.log");
            await WaitForHttpAsync(WebUrl + "/Identity/Account/Login");
            await WaitForHttpAsync(FunctionUrl + "/api/home/summary");
            await File.WriteAllTextAsync(Path.Combine(EvidenceDirectory, "environment.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                startedAtUtc = DateTime.UtcNow, webHost = WebUrl, functionsHost = FunctionUrl,
                database = databaseName, storage = "local emulator", emailEnabled = false,
                note = "Real published web app and Azure Functions Core Tools with an isolated SQL Server database. No live Azure services used."
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch { await DisposeAsync(); throw; }
    }

    public ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).Options);
    public HttpClient Client() => new(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false })
        { BaseAddress = new Uri(WebUrl), Timeout = TimeSpan.FromSeconds(30) };

    private async Task PublishAsync(string project, string output)
    {
        var process = Start("dotnet", new[] { "publish", Path.Combine(Root, project), "-c", "Release", "-o", output }, Root, new(), "publish-" + Path.GetFileNameWithoutExtension(project) + ".log");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)); await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode != 0) throw new InvalidOperationException("Publish failed. See evidence publish logs.");
    }
    private Process Start(string command, IEnumerable<string> arguments, string working, Dictionary<string, string> environment, string logName, string? overrideName = null)
    {
        command = (overrideName == null ? null : Environment.GetEnvironmentVariable(overrideName)) ?? command;
        var info = new ProcessStartInfo(command) { WorkingDirectory = working, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        if (OperatingSystem.IsWindows() && (command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || command == "azurite"))
        { info.FileName = "cmd.exe"; info.ArgumentList.Add("/d"); info.ArgumentList.Add("/c"); info.ArgumentList.Add(command); }
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        foreach (var pair in environment) info.Environment[pair.Key] = pair.Value;
        var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start " + command);
        processes.Add(process);
        var gate = new object(); var path = Path.Combine(EvidenceDirectory, logName);
        async Task CopyAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
                lock (gate) File.AppendAllText(path, line + Environment.NewLine);
        }
        logTasks.Add(CopyAsync(process.StandardOutput)); logTasks.Add(CopyAsync(process.StandardError));
        return process;
    }
    private async Task WaitForHttpAsync(string url)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        var deadline = DateTime.UtcNow.AddSeconds(150);
        while (DateTime.UtcNow < deadline)
        {
            try { if ((await client.GetAsync(url)).IsSuccessStatusCode) return; } catch (HttpRequestException) { } catch (TaskCanceledException) { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Test host did not become ready: {url}. Inspect web.log/functions.log in {EvidenceDirectory}.");
    }
    private static int FreePort() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
    private static async Task<bool> PortOpenAsync(int port) { try { using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, port); return true; } catch (SocketException) { return false; } }
    private static async Task WaitForPortAsync(int port) { for (var i = 0; i < 100; i++) { if (await PortOpenAsync(port)) return; await Task.Delay(250); } throw new TimeoutException("Azurite did not start. Install it or set GOTG_AZURITE_PATH."); }
    private static string FindRoot()
    {
        var configured = Environment.GetEnvironmentVariable("GOTG_REPO_ROOT");
        if (configured != null) return Path.GetFullPath(configured);
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "GiftOfTheGivers.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot find GiftOfTheGivers.sln. Set GOTG_REPO_ROOT.");
    }
    public async Task DisposeAsync()
    {
        foreach (var process in processes.AsEnumerable().Reverse())
        { try { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } catch (InvalidOperationException) { } }
        await Task.WhenAll(logTasks);
        if (ownsDatabase)
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(masterConnection); await connection.OpenAsync();
            await using var command = connection.CreateCommand(); command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync(); ownsDatabase = false;
        }
    }
}
