using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KodeRunner;
using Tuvalu.logger;

namespace KodeRunner
{
    #region StartBuildProcess
    public class BuildProcess : IRunnable, IDisposable, IAsyncDisposable
    {
        public string Name => "Build Process";
        public string Language => "ANY";
        public int Priority => 9999999;
        public string description =>
            "Executes build process for all languages that have been registered for IRunnable";

        private readonly Process _process;
        public event Action<string> OnOutput;
        public List<string> CommentRegexes = new List<string>
        {
            @"^#.*$",
            @"^//.*$",
            @"^/\*.*\*/$",
            @"^<!--.*-->$",
            @"^\"".*\""$",
            @"^//.*$",
            @"^<!--.*-->$",
            @"^/\*.*\*/$",
            @"^;.*$",
            @"^--",
            @"^REM.*$",
            @"^::.*$",
            @"^@REM.*$",
            @"^@ECHO.*$",
            @"^@.*$",
            @"^#.*$",
            @"^//.*$",
            @"^/\*.*\*/$",
            @"^<!--.*-->$",
        };

        public BuildProcess()
        {
            Logger.Log("Initializing BuildProcess...");
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "./bash",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false,
                },
            };

            _ = _process.Start();
            StartOutputReader();
            Logger.Log("BuildProcess initialized.");
        }

        public void Dispose()
        {
            Logger.Log("Disposing BuildProcess...");
            _process.Kill();
            _process.Dispose();
            Logger.Log("BuildProcess disposed.");
        }

        public async ValueTask DisposeAsync()
        {
            Logger.Log("Disposing BuildProcess asynchronously...");
            _process.Kill();
            _process.Dispose();
            await Task.CompletedTask;
            Logger.Log("BuildProcess disposed asynchronously.");
        }

        private void StartOutputReader()
        {
            Logger.Log("Starting output reader...");
            _ = Task.Run(async () =>
            {
                var buffer = new byte[1024];
                while (!_process.HasExited)
                {
                    int read = await _process.StandardOutput.BaseStream.ReadAsync(
                        buffer,
                        0,
                        buffer.Length
                    );
                    if (read > 0)
                    {
                        string output = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
                        OnOutput?.Invoke(output);
                        Logger.Log($"Output: {output}");
                    }
                }
            });
        }

        public void SetupCodeDir()
        {
            Logger.Log("Setting up code directories...");
            var directories = new List<string>
            {
                Core.CodeDir,
                Core.BuildDir,
                Core.TempDir,
                Core.OutputDir,
                Core.LogDir,
                Core.RunnableDir,
            };

            foreach (var dir in directories)
            {
                string path = Path.Combine(Core.RootDir, dir);
                if (!Directory.Exists(path))
                {
                    _ = Directory.CreateDirectory(path);
                    Logger.Log($"Directory created: {path}");
                }
            }
        }

        public async Task SendInput(string input)
        {
            Logger.Log($"Sending input to build process: {input}");
            await _process.StandardInput.WriteLineAsync(input);
        }

        public void Execute(Provider.ISettingsProvider settings)
        {
            Logger.Log("Executing build process...");
            throw new NotImplementedException();
        }
    }
}
    #endregion
