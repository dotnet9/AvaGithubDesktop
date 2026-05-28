using System.Diagnostics;
using System.Text;

namespace AvaGithubDesktop.Core.Services;

internal static class GitCommandRunner
{
    public static Task<string> RunRequiredAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        return RunTextAsync(workingDirectory, arguments, cancellationToken, allowFailure: false);
    }

    public static Task<string> RunOptionalAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        return RunTextAsync(workingDirectory, arguments, cancellationToken, allowFailure: true);
    }

    public static async Task<bool> RunToFileAsync(
        string workingDirectory,
        string outputPath,
        CancellationToken cancellationToken,
        IReadOnlyList<string> arguments)
    {
        var commandText = GitCommandLog.FormatCommand(workingDirectory, arguments);
        var stopwatch = Stopwatch.StartNew();
        GitCommandLog.LogStarted(commandText);
        var startInfo = CreateStartInfo(workingDirectory, arguments);
        startInfo.RedirectStandardOutput = true;

        int exitCode;
        long outputLength;
        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start git.");

            await using var outputStream = File.Create(outputPath);
            var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await stdoutTask;
            await outputStream.FlushAsync(cancellationToken);
            await stderrTask;
            exitCode = process.ExitCode;
            outputLength = new FileInfo(outputPath).Length;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            GitCommandLog.LogFailed(commandText, stopwatch.Elapsed, ex);
            throw;
        }

        stopwatch.Stop();
        GitCommandLog.LogCompleted(commandText, exitCode, stopwatch.Elapsed);

        return exitCode == 0 && outputLength > 0;
    }

    private static async Task<string> RunTextAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool allowFailure)
    {
        var commandText = GitCommandLog.FormatCommand(workingDirectory, arguments);
        var stopwatch = Stopwatch.StartNew();
        GitCommandLog.LogStarted(commandText);
        var startInfo = CreateStartInfo(workingDirectory, arguments);
        startInfo.RedirectStandardOutput = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;

        int exitCode;
        string stdout;
        string stderr;
        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start git.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            stdout = (await stdoutTask).Trim();
            stderr = (await stderrTask).Trim();
            exitCode = process.ExitCode;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            GitCommandLog.LogFailed(commandText, stopwatch.Elapsed, ex);
            throw;
        }

        stopwatch.Stop();
        GitCommandLog.LogCompleted(commandText, exitCode, stopwatch.Elapsed);

        if (exitCode != 0 && !allowFailure)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "Git command failed." : stderr);
        }

        return exitCode == 0 ? stdout : string.Empty;
    }

    private static ProcessStartInfo CreateStartInfo(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["GIT_EDITOR"] = "true";
        startInfo.Environment["GIT_MERGE_AUTOEDIT"] = "no";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
