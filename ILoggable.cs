using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using KodeRunner;

namespace Tuvalu.logger
{
    public class Logger
    {
        private static readonly SemaphoreSlim _logLock = new SemaphoreSlim(1, 1);

        static readonly string time = DateTime.Now.ToString("yyyy-MM-dd");
        static readonly string logPath = Path.Combine(Core.RootDir, Core.LogDir, $"{time}.log");

        static Logger()
        {
            if (File.Exists(logPath))
            {
                var lastLine = File.ReadLines(logPath).LastOrDefault();
                if (lastLine == null || !lastLine.StartsWith("=== Restart"))
                {
                    AppendRestartSeparator();
                }
            }
            else
            {
                AppendRestartSeparator();
            }
        }

        private static void AppendRestartSeparator()
        {
            string separator = $"=== Restart {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n";
            File.AppendAllText(logPath, separator);
        }

        public struct LogEntry
        {
            public string Message;
            public string Level;
            public string Timestamp;
        }

        public static void Log(LogEntry entry)
        {
            string logEntry = $"{entry.Timestamp} - {entry.Level}: {entry.Message}\n";
            File.AppendAllText(logPath, logEntry);
        }

        public static void Log(string message, string level)
        {
            var entry = new LogEntry
            {
                Message = message,
                Level = level,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            Log(entry);
        }

        public static void Log(string message)
        {
            Log(message, "INFO");
        }

        public static void Log(Exception ex)
        {
            LogEntry entry = new LogEntry
            {
                Message = ex.Message,
                Level = "ERROR",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            Log(entry);
        }

        public static void Log(string message, Exception ex)
        {
            LogEntry entry = new LogEntry
            {
                Message = $"{message}: {ex.Message}",
                Level = "ERROR",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            Log(entry);
        }

        public static void Log(string message, string level, string customLogPath)
        {
            LogEntry entry = new LogEntry
            {
                Message = message,
                Level = level,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };
            string logEntry = $"{entry.Timestamp} - {entry.Level}: {entry.Message}\n";
            File.AppendAllText(customLogPath, logEntry);
        }

        public static async Task LogAsync(LogEntry entry)
        {
            await _logLock.WaitAsync();
            try
            {
                string logEntry = $"{entry.Timestamp} - {entry.Level}: {entry.Message}\n";
                await File.AppendAllTextAsync(logPath, logEntry);
            }
            finally
            {
                _ = _logLock.Release();
            }
        }

        public static async Task LogAsync(string message)
        {
            await LogAsync(
                new LogEntry
                {
                    Message = message,
                    Level = "INFO",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                }
            );
        }

        public static async Task LogAsync(Exception ex)
        {
            await LogAsync(
                new LogEntry
                {
                    Message = ex.Message,
                    Level = "ERROR",
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                }
            );
        }
    }
}
