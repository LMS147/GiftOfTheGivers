using System.Diagnostics;

namespace GiftOfTheGivers.AssignmentTests.Infrastructure;

// Visual Studio does not launch npm .cmd shims through a shell automatically.
public static class TestProcessLauncher
{
    public static ProcessStartInfo Create(string command, IEnumerable<string> arguments, string working)
    {
        var args = arguments.ToArray();
        command = Resolve(command);
        var info = new ProcessStartInfo(command)
        {
            WorkingDirectory = working, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        if (OperatingSystem.IsWindows() && (command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)))
        {
            // ArgumentList applies Windows executable quoting, which is different from cmd /c quoting.
            // Expand private child environment variables inside quotes to preserve spaces and ampersands.
            if (args.Append(command).Any(value => value.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0))
                throw new ArgumentException("Test batch-tool paths and arguments must not contain quotes or newlines.");
            info.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            info.Environment["GOTG_TOOL_COMMAND"] = command;
            for (var i = 0; i < args.Length; i++) info.Environment[$"GOTG_TOOL_ARG_{i}"] = args[i];
            info.Arguments = "/d /s /v:off /c \"\"%GOTG_TOOL_COMMAND%\"" +
                string.Concat(Enumerable.Range(0, args.Length).Select(i => $" \"%GOTG_TOOL_ARG_{i}%\"")) + "\"";
        }
        else foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    private static string Resolve(string command)
    {
        command = Environment.ExpandEnvironmentVariables(command);
        if (File.Exists(command)) return Path.GetFullPath(command);
        if (!OperatingSystem.IsWindows()) return command;
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"));
        var extensions = Path.HasExtension(command) ? new[] { "" } : new[] { ".exe", ".cmd", ".bat", ".com" };
        foreach (var directory in directories.Where(d => !string.IsNullOrWhiteSpace(d)))
            foreach (var extension in extensions)
            {
                var path = Path.Combine(directory.Trim('"'), command + extension);
                if (File.Exists(path)) return Path.GetFullPath(path);
            }
        throw new FileNotFoundException($"Cannot find test tool '{command}'. Install the tool, restart Visual Studio, or set its GOTG_AZURITE_PATH / GOTG_FUNC_PATH override. See Testing/Assignment-A.md.");
    }
}
