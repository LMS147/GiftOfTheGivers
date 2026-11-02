using System.Text.Json;
using System.Diagnostics;

namespace GiftOfTheGivers.AssignmentTests.Infrastructure;

// Visual Studio does not launch npm .cmd shims through a shell automatically.
public static class TestProcessLauncher
{
    public static ProcessStartInfo Create(string command, IEnumerable<string> arguments, string working)
    {
        var args = arguments.ToArray();
        command = Resolve(command);
        if (OperatingSystem.IsWindows() && command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            var npmTool = ResolveNpmTool(command);
            if (npmTool is { } tool)
            {
                command = tool.Command;
                if (tool.Script != null) args = new[] { tool.Script }.Concat(args).ToArray();
            }
        }
        var info = new ProcessStartInfo(command)
        {
            WorkingDirectory = working, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        if (OperatingSystem.IsWindows() && (command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || command.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)))
        {
            // ArgumentList applies Windows executable quoting, which is different from cmd /c quoting.
            // Keep the executable path literal so batch %~dp0 resolves to the tool directory.
            // Expand argument variables inside quotes to preserve spaces and ampersands.
            if (args.Append(command).Any(value => value.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0))
                throw new ArgumentException("Test batch-tool paths and arguments must not contain quotes or newlines.");
            info.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            for (var i = 0; i < args.Length; i++) info.Environment[$"GOTG_TOOL_ARG_{i}"] = args[i];
            info.Arguments = "/d /s /v:off /c \"\"" + command + "\"" +
                string.Concat(Enumerable.Range(0, args.Length).Select(i => $" \"%GOTG_TOOL_ARG_{i}%\"")) + "\"";
        }
        else foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    private static (string Command, string? Script)? ResolveNpmTool(string shim)
    {
        var name = Path.GetFileNameWithoutExtension(shim);
        var package = name.Equals("azurite", StringComparison.OrdinalIgnoreCase) ? "azurite"
            : name.Equals("func", StringComparison.OrdinalIgnoreCase) ? "azure-functions-core-tools" : null;
        if (package == null) return null;
        var directory = Path.GetDirectoryName(shim)!;
        // Local npm installs put shims in node_modules/.bin; global installs put them beside node_modules.
        var candidates = new[] { Path.Combine(directory, "..", package), Path.Combine(directory, "node_modules", package) };
        foreach (var candidate in candidates)
        {
            var packageDirectory = Path.GetFullPath(candidate);
            var manifestPath = Path.Combine(packageDirectory, "package.json");
            if (!File.Exists(manifestPath)) continue;
            var nativeFunctionHost = Path.Combine(packageDirectory, "bin", "func.exe");
            if (name.Equals("func", StringComparison.OrdinalIgnoreCase) && File.Exists(nativeFunctionHost))
                return (nativeFunctionHost, null);
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!manifest.RootElement.TryGetProperty("bin", out var bins)) continue;
            var relative = bins.ValueKind == JsonValueKind.String ? bins.GetString()
                : bins.TryGetProperty(name, out var bin) ? bin.GetString() : null;
            if (relative == null) continue;
            var script = Path.GetFullPath(Path.Combine(packageDirectory, relative));
            if (File.Exists(script) && script.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                return (Resolve("node.exe"), script);
        }
        return null;
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
