using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KodeRunner;
<<<<<<< HEAD
using Tuvalu.logger;
=======

>>>>>>> 3b45ddc (forgot the html files which we realy need)
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

            _process.Start();
            StartOutputReader();
        }

        public void Dispose()
        {
            _process.Kill();
            _process.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            _process.Kill();
            _process.Dispose();
            await Task.CompletedTask;
        }

        private void StartOutputReader()
        {
            Task.Run(async () =>
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
                    }
                }
            });
        }

        public void SetupCodeDir()
        {
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
<<<<<<< HEAD
                Directory.CreateDirectory(Cpath);
                Logger.Log(Core.LoggerHandle + "created Code path");
            }
            if (!Directory.Exists(Bpath))
            {
                Directory.CreateDirectory(Bpath);
                Logger.Log(Core.LoggerHandle + "Created 
            }
            if (!Directory.Exists(Tpath))
            {
                Directory.CreateDirectory(Tpath);
            }
            if (!Directory.Exists(Opath))
            {
                Directory.CreateDirectory(Opath);
            }
            if (!Directory.Exists(Lpath))
            {
                Directory.CreateDirectory(Lpath);
            }
            if (!Directory.Exists(Rpath))
            {
                Directory.CreateDirectory(Rpath);
            }
            

=======
                string path = Path.Combine(Core.RootDir, dir);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
>>>>>>> 3b45ddc (forgot the html files which we realy need)
        }

        public async Task SendInput(string input)
        {
            await _process.StandardInput.WriteLineAsync(input);
        }

        public void Execute(Provider.ISettingsProvider settings)
        {
            throw new NotImplementedException();
        }
    }
}
    #endregion
