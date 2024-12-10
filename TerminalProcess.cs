using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text;

namespace KodeRunner
{
    public class TerminalProcess
    {
        #pragma warning disable CS8618
        public event Action<string> OnOutput;
        #pragma warning restore CS8618

        public async Task<int> ExecuteCommand(string command)
        {
            var tcs = new TaskCompletionSource<int>();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                },
                EnableRaisingEvents = true
            };

            process.Exited += (sender, args) =>
            {
                tcs.SetResult(process.ExitCode);
                process.Dispose();
            };

            _ = process.Start();

            // Read standard output asynchronously
            _ = Task.Run(async () =>
            {
                var buffer = new char[1];
                while (!process.StandardOutput.EndOfStream)
                {
                    int read = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        OnOutput?.Invoke(new string(buffer, 0, read));
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
                        OnOutput?.Invoke(new string(buffer, 0, read));
                    }
                }
            });

            await process.WaitForExitAsync();

            return await tcs.Task;
        }
    }
}
