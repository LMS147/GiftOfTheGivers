using System.Diagnostics;
using GiftOfTheGivers.AssignmentTests.Infrastructure;
using Xunit;

namespace GiftOfTheGivers.AssignmentTests.Unit;

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Exercises Windows cmd.exe and npm shim quoting."; }
}

[Trait("Category", "Unit")]
public class TestProcessLauncherTests
{
    [WindowsFact]
    public async Task BatchToolPathsAndArgumentsPreserveSpacesAndAmpersands()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gotg tools & tests " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var command = Path.Combine(directory, "example tool.cmd");
            await File.WriteAllTextAsync(command, "@echo off\r\necho ROOT=%~dp0\r\necho ARG=%1\r\n");
            var argument = Path.Combine(directory, "storage data");
            using var process = Process.Start(TestProcessLauncher.Create(command, new[] { argument }, directory))!;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = await process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);
            Assert.Contains("ROOT=" + directory + Path.DirectorySeparatorChar, output);
            Assert.Contains("ARG=\"" + argument + "\"", output);
            Assert.True(string.IsNullOrWhiteSpace(error), error);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
