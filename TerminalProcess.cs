using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace KodeRunner
{
    public class TerminalProcess : IAsyncDisposable
    {
#pragma warning disable CS8618
        public event Action<string> OnOutput;
#pragma warning restore CS8618


        // funciton to clear the buffer when the process is done
        public async Task ClearBuffer()
        {
            await Task.Delay(1000);
            OnOutput?.Invoke("\n");
        }

        // Add static process tracking
        private static ConcurrentDictionary<int, Process> ActiveProcesses =
            new ConcurrentDictionary<int, Process>();

        private void SendOutput(string output)
        {
            if (string.IsNullOrEmpty(output))
                return;
            var parsedOutput = TerminalCodeParser.ParseToResonite(output);
            OnOutput?.Invoke(parsedOutput);
        }

        public bool SendInput(string input)
        {
            if (ActiveProcesses.Count == 0)
                return false;

            try
            {
                var process = ActiveProcesses[ActiveProcesses.Keys.First()];
                if (process.StartInfo.RedirectStandardInput)
                {
                    process.StandardInput.WriteLine(input);
                    process.StandardInput.Flush();
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            StopAllProcesses();
            await Task.CompletedTask;
        }

        public async Task<int> ExecuteCommand(string command)
        {
            var tcs = new TaskCompletionSource<int>();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "./bash",
                    Arguments = OperatingSystem.IsWindows()
                        ? $"-Command \"{command}\""
                        : $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                },
                EnableRaisingEvents = true,
            };

            process.Exited += (sender, args) =>
            {
                ActiveProcesses.TryRemove(process.Id, out _);
                tcs.SetResult(process.ExitCode);
                process.Dispose();
            };

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    foreach (char c in args.Data)
                    {
                        SendOutput(c.ToString());
                    }
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    foreach (char c in args.Data)
                    {
                        SendOutput(c.ToString());
                    }
                }
            };

            process.Start();
            ActiveProcesses.TryAdd(process.Id, process);

            // Read standard output asynchronously
            _ = Task.Run(async () =>
            {
                var buffer = new char[1];
                while (!process.StandardOutput.EndOfStream)
                {
                    int read = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        OnOutput?.Invoke(buffer[0].ToString());
                    }
                }
            });

            // Read standard error asynchronously
            _ = Task.Run(async () =>
            {
                var buffer = new char[1];
                while (!process.StandardError.EndOfStream)
                {
                    int read = await process.StandardError.ReadAsync(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        OnOutput?.Invoke(buffer[0].ToString());
                    }
                }
            });

            await process.WaitForExitAsync();

            return await tcs.Task;
        }

        // Add static method to stop all processes
        public static void StopAllProcesses()
        {
            foreach (var processEntry in ActiveProcesses)
            {
                try
                {
                    var process = processEntry.Value;
                    if (!process.HasExited)
                    {
                        process.Kill(true); // Force kill the process and its children
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping process {processEntry.Key}: {ex.Message}");
                }
            }
            ActiveProcesses.Clear();
        }
    }
}
