using System.IO;
using System.Diagnostics;
using System.Text;

namespace DesktopOrganizer.UI.Utilities;

public static class Logger
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
    private static readonly object _lock = new();

    public static void Log(string message, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string filePath = "")
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var fileName = Path.GetFileName(filePath);
        var logLine = $"[{timestamp}] [{fileName}:{memberName}] {message}";

        Debug.WriteLine(logLine);

        try
        {
            lock (_lock)
            {
                File.AppendAllText(LogPath, logLine + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write to log file: {ex.Message}");
        }
    }

    public static void LogError(string message, Exception? ex = null, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "", [System.Runtime.CompilerServices.CallerFilePath] string filePath = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine(message);
        if (ex != null)
        {
            sb.AppendLine($"Exception: {ex.GetType().Name}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"StackTrace: {ex.StackTrace}");
        }

        Log($"[ERROR] {sb.ToString().TrimEnd()}", memberName, filePath);
    }
}
