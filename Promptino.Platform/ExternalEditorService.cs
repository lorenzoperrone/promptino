using System.Diagnostics;
using System.Collections.Generic;
using System.IO;

namespace Promptino.Platform;

public interface IProcessLauncher
{
    void Launch(string fileName, IEnumerable<string> arguments, string? workingDirectory = null);
}

public sealed class SystemProcessLauncher : IProcessLauncher
{
    public void Launch(string fileName, IEnumerable<string> arguments, string? workingDirectory = null)
    {
        var resolvedPath = ResolveExecutablePath(fileName, workingDirectory);
        var startInfo = new ProcessStartInfo
        {
            FileName = resolvedPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }
        using var _ = Process.Start(startInfo);
    }

    private static string ResolveExecutablePath(string fileName, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return fileName;

        // If it's already an absolute path that exists, return it
        if (Path.IsPathRooted(fileName))
        {
            if (File.Exists(fileName))
                return fileName;

            var ext = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext))
            {
                foreach (var searchExt in new[] { ".exe", ".cmd", ".bat", ".com" })
                {
                    var fullPath = fileName + searchExt;
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }
            return fileName;
        }

        // Try local files first (relative path)
        var relativePathsToCheck = new List<string>();
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            relativePathsToCheck.Add(Path.Combine(workingDirectory, fileName));
        }
        relativePathsToCheck.Add(Path.Combine(Directory.GetCurrentDirectory(), fileName));

        foreach (var relPath in relativePathsToCheck)
        {
            if (File.Exists(relPath))
                return Path.GetFullPath(relPath);

            var ext = Path.GetExtension(relPath);
            if (string.IsNullOrEmpty(ext))
            {
                foreach (var searchExt in new[] { ".exe", ".cmd", ".bat", ".com" })
                {
                    var fullPath = relPath + searchExt;
                    if (File.Exists(fullPath))
                        return Path.GetFullPath(fullPath);
                }
            }
        }

        // Search the PATH environment variable
        var pathEnv = System.Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            var paths = pathEnv.Split(Path.PathSeparator, System.StringSplitOptions.RemoveEmptyEntries);
            var searchExtensions = new[] { "", ".exe", ".cmd", ".bat", ".com" };
            
            var fileExt = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(fileExt))
            {
                // If it already has an extension, check that first
                searchExtensions = new[] { "" };
            }
            else
            {
                // Otherwise check standard executable extensions
                searchExtensions = new[] { ".exe", ".cmd", ".bat", ".com" };
            }

            foreach (var dir in paths)
            {
                foreach (var ext in searchExtensions)
                {
                    try
                    {
                        var fullPath = Path.Combine(dir, fileName + ext);
                        if (File.Exists(fullPath))
                        {
                            return Path.GetFullPath(fullPath);
                        }
                    }
                    catch
                    {
                        // Ignore invalid characters in path
                    }
                }
            }
        }

        return fileName;
    }
}

public interface IExternalEditorService
{
    bool TryOpenScript(string? scriptPath, string? configuredEditorPath, out string warningMessage);
}

public sealed class ExternalEditorService : IExternalEditorService
{
    public const string DefaultEditorFileName = "notepad.exe";

    private readonly IProcessLauncher _launcher;

    public ExternalEditorService(IProcessLauncher? launcher = null)
    {
        _launcher = launcher ?? new SystemProcessLauncher();
    }

    public bool TryOpenScript(string? scriptPath, string? configuredEditorPath, out string warningMessage)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            warningMessage = "Load a script first, then try editing again.";
            return false;
        }

        if (!File.Exists(scriptPath))
        {
            warningMessage = "The script file could not be found on disk. Reload or save it, then try again.";
            return false;
        }

        string editorExe;
        List<string> editorArgs;
        if (string.IsNullOrWhiteSpace(configuredEditorPath))
        {
            editorExe = DefaultEditorFileName;
            editorArgs = new List<string>();
        }
        else
        {
            (editorExe, editorArgs) = ParseCommandLine(configuredEditorPath);
        }

        if (Directory.Exists(editorExe))
        {
            warningMessage = "The configured editor path is a directory, not an executable file.";
            return false;
        }

        string? workingDir = null;
        try
        {
            workingDir = Path.GetDirectoryName(scriptPath);
        }
        catch
        {
            // Ignore path format issues, launcher will handle it
        }

        var arguments = new List<string>(editorArgs) { scriptPath };

        try
        {
            _launcher.Launch(editorExe, arguments, workingDir);
            warningMessage = string.Empty;
            return true;
        }
        catch
        {
            warningMessage = $"Could not launch \"{editorExe}\". Check the external editor path in settings.";
            return false;
        }
    }

    public static (string Executable, List<string> Arguments) ParseCommandLine(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return (string.Empty, new List<string>());

        commandLine = commandLine.Trim();

        // 1. Starts with a quote
        if (commandLine.StartsWith("\""))
        {
            int closingQuoteIndex = commandLine.IndexOf("\"", 1);
            if (closingQuoteIndex > 0)
            {
                string exe = commandLine.Substring(1, closingQuoteIndex - 1).Trim();
                string argsStr = commandLine.Substring(closingQuoteIndex + 1).Trim();
                var args = ParseArguments(argsStr);
                return (exe, args);
            }
            else
            {
                return (commandLine.Substring(1).Trim(), new List<string>());
            }
        }

        // 2. Check for standard extensions in unquoted string
        var exts = new[] { ".exe", ".cmd", ".bat", ".lnk" };
        foreach (var ext in exts)
        {
            int extIndex = commandLine.IndexOf(ext, StringComparison.OrdinalIgnoreCase);
            if (extIndex > 0)
            {
                int endIdx = extIndex + ext.Length;
                if (endIdx == commandLine.Length || commandLine[endIdx] == ' ')
                {
                    string exe = commandLine.Substring(0, endIdx).Trim();
                    string argsStr = commandLine.Substring(endIdx).Trim();
                    var args = ParseArguments(argsStr);
                    return (exe, args);
                }
            }
        }

        // 3. Unquoted path containing spaces. Search for the first prefix that exists as a file.
        //    Try longest prefix first to avoid matching a subdirectory name.
        var candidates = new List<(int Index, string Path)>();
        int spaceIdx = -1;
        while ((spaceIdx = commandLine.IndexOf(' ', spaceIdx + 1)) != -1)
        {
            string prefix = commandLine.Substring(0, spaceIdx).Trim();
            if (prefix.Length > 0)
                candidates.Add((spaceIdx, prefix));
        }
        // Check from longest to shortest so "C:\Program Files\editor.exe" is found before "C:\Program"
        candidates.Sort((a, b) => b.Path.Length.CompareTo(a.Path.Length));
        foreach (var (_, prefix) in candidates)
        {
            if (File.Exists(prefix) || File.Exists(prefix + ".exe"))
            {
                string argsStr = commandLine.Substring(prefix.Length).Trim();
                var args = ParseArguments(argsStr);
                return (prefix, args);
            }
        }

        // 4. Fallback: split by the first space
        int firstSpace = commandLine.IndexOf(' ');
        if (firstSpace > 0)
        {
            string exe = commandLine.Substring(0, firstSpace).Trim();
            string argsStr = commandLine.Substring(firstSpace + 1).Trim();
            var args = ParseArguments(argsStr);
            return (exe, args);
        }

        return (commandLine, new List<string>());
    }

    private static List<string> ParseArguments(string argsStr)
    {
        var args = new List<string>();
        if (string.IsNullOrWhiteSpace(argsStr))
            return args;

        bool inQuotes = false;
        var currentArg = new System.Text.StringBuilder();
        for (int i = 0; i < argsStr.Length; i++)
        {
            char c = argsStr[i];
            if (c == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (currentArg.Length > 0)
                {
                    args.Add(currentArg.ToString());
                    currentArg.Clear();
                }
            }
            else
            {
                currentArg.Append(c);
            }
        }
        if (currentArg.Length > 0)
        {
            args.Add(currentArg.ToString());
        }
        return args;
    }
}
