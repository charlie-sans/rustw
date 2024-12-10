using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KodeRunner
{
    #region Implementations
    public class DotnetRunnable : IRunnable
    {
        public string Name => "dotnet";
        public string Language => "csharp";
        public int Priority => 0;
        public string description => "Executes dotnet projects";

        public void Execute(Provider.ISettingsProvider settings)
        {
            Console.WriteLine("Running dotnet");
        }
    }

    [Runnable("dotnet", "csharp", 1)]
    public class ModifiedDotnetRunnable : IRunnable
    {
        public string Name => "dotnet";
        public string Language => "csharp";
        public int Priority => 1;
        public string description => "Executes dotnet projects with metadata";

        public void Execute(Provider.ISettingsProvider settings)
        {
            Console.WriteLine($"Running dotnet project: {settings.ProjectName}");
            Console.WriteLine($"Project Path: {settings.ProjectPath}");

            var terminalProcess = new TerminalProcess();

            // Capture the PMS WebSocket from settings
            WebSocket pmsWebSocket = settings.PmsWebSocket;

            terminalProcess.OnOutput += async (output) =>
            {
                Console.WriteLine(output);
                if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(output);
                    await pmsWebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            };

            var buildCommand = $"dotnet build \"{settings.ProjectPath}\"";
            var runCommand = $"dotnet run --project \"{settings.ProjectPath}\"";

            terminalProcess.ExecuteCommand(buildCommand).Wait();

            if (settings.Run_On_Build)
            {
                terminalProcess.ExecuteCommand(runCommand).Wait();
            }
        }
    }

    public class PythonRunnable : IRunnable
    {
        public string Name => "python";
        public string Language => "python";
        public int Priority => 0;
        public string description => "Executes python projects";

        public void Execute(Provider.ISettingsProvider settings)
        {
            Console.WriteLine("Running python");
        }
    }

    [Runnable("python", "python", 0)]
    public class ModifiedPythonRunnable : IRunnable
    {
        public string Name => "python";
        public string Language => "python";
        public int Priority => 0;
        public string description => "Executes python projects with metadata";

        public void Execute(Provider.ISettingsProvider settings)
        {
            Console.WriteLine("Running python with metadata");
            Console.WriteLine($"Project Name: {settings.ProjectName}");
            Console.WriteLine($"Project Path: {settings.ProjectPath}");

        }
    }

    // C runnable
    [Runnable("c", "clang", 0)]
    public class CRunnable : IRunnable
    {
        public string Name => "c";
        public string Language => "c";
        public int Priority => 0;
        public string description => "Executes C projects";

        public void Execute(Provider.ISettingsProvider settings)
        {
            Console.WriteLine("Running C project");

            var terminalProcess = new TerminalProcess();

            // Capture the PMS WebSocket from settings
            WebSocket pmsWebSocket = settings.PmsWebSocket;

            terminalProcess.OnOutput += async (output) =>
            {
                Console.WriteLine(output);
                if (pmsWebSocket != null && pmsWebSocket.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(output);
                    await pmsWebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            };

            var codePath = Path.Combine(Core.RootDir, Core.CodeDir, settings.ProjectName);
            var outputFilePath = Path.Combine(codePath, settings.Output);
            var mainFilePath = Path.Combine(codePath, settings.Main_File);

            var buildCommand = $"clang -o \"{outputFilePath}\" \"{mainFilePath}\"";
            Console.WriteLine($"Build command: {buildCommand}");

            // Execute the build command
            terminalProcess.ExecuteCommand(buildCommand).Wait();

            if (settings.Run_On_Build)
            {
                // Run the project
                var runCommand = $"\"{outputFilePath}\"";
                terminalProcess.ExecuteCommand(runCommand).Wait();
            }
        }
    }

    #endregion

}