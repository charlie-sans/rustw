using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace KodeRunner
{
   #region StartBuildProcess
    public class BuildProcess : IRunnable, IDisposable
    {
        public string Name => "Build Process";
        public string Language => "ANY";
        public int Priority => 0;
        public string description => "Executes build process for all languages that have been registered for IRunnable";

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
            @"^--"
        };
        public BuildProcess()
        {
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash", // Use "cmd.exe" for Windows
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false
                }
            };
            _process.Start();
            StartOutputReader();
        }

        private void StartOutputReader()
        {
            Task.Run(async () =>
            {
                var buffer = new byte[1024];
                while (!_process.HasExited)
                {
                    int read = await _process.StandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        string output = Encoding.UTF8.GetString(buffer, 0, read);
                        OnOutput?.Invoke(output);
                    }
                }
            });
        }

        public async Task SendInput(string input)
        {
            await _process.StandardInput.WriteLineAsync(input);
        }
        public void Execute(Provider.ISettingsProvider settings)
        {
            throw new NotImplementedException();
        }
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

}
#endregion