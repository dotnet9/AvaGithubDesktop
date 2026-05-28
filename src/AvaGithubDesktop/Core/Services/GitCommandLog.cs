using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CodeWF.Log.Core;

namespace AvaGithubDesktop.Core.Services;

internal static class GitCommandLog
{
    private static readonly Regex UrlCredentialsRegex = new(
        @"(?<scheme>[A-Za-z][A-Za-z0-9+.-]*://)[^/\s@]+@",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SensitiveQueryRegex = new(
        @"(?<prefix>[?&](?:access_token|auth|password|token|signature|sig)=)[^&\s]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string FormatCommand(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder();
        builder.Append("git -C ");
        builder.Append(QuoteArgument(workingDirectory, forceQuote: true));

        foreach (var argument in arguments)
        {
            builder.Append(' ');
            builder.Append(QuoteArgument(argument));
        }

        return builder.ToString();
    }

    public static void LogStarted(string commandText)
    {
        WriteInfo($"[git] $ {commandText}");
    }

    public static void LogCompleted(string commandText, int exitCode, TimeSpan elapsed)
    {
        WriteInfo($"[git] exit {exitCode} in {FormatElapsed(elapsed)}: {commandText}");
    }

    public static void LogTimedOut(string commandText, TimeSpan elapsed)
    {
        WriteInfo($"[git] timed out in {FormatElapsed(elapsed)}: {commandText}");
    }

    public static void LogFailed(string commandText, TimeSpan elapsed, Exception exception)
    {
        var failure = exception is OperationCanceledException ? "canceled" : "failed";
        WriteInfo($"[git] {failure} in {FormatElapsed(elapsed)}: {commandText} ({NormalizeLogText(exception.Message)})");
    }

    private static void WriteInfo(string message)
    {
        Logger.Info(message, message, log2UI: true, log2File: true, log2Console: false);
    }

    private static string QuoteArgument(string argument, bool forceQuote = false)
    {
        var display = NormalizeLogText(RedactSensitiveValues(argument));
        if (display.Length == 0)
        {
            return "\"\"";
        }

        if (!forceQuote && !display.Any(RequiresQuoting))
        {
            return display;
        }

        return $"\"{display.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static bool RequiresQuoting(char value)
    {
        return char.IsWhiteSpace(value)
               || value is '"' or '\'' or '&' or '|' or '<' or '>' or ';' or '(' or ')' or '$' or '`';
    }

    private static string RedactSensitiveValues(string value)
    {
        var withoutCredentials = UrlCredentialsRegex.Replace(value, "${scheme}***@");
        return SensitiveQueryRegex.Replace(withoutCredentials, "${prefix}***");
    }

    private static string NormalizeLogText(string value)
    {
        return value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalMilliseconds < 1000)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{Math.Max(0, elapsed.TotalMilliseconds):0} ms");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{elapsed.TotalSeconds:0.0} s");
    }
}
