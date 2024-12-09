using System;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using KodeRunner;
class TerminalProcess : IDisposable
{
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
    public TerminalProcess()
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/zsh", // Use "cmd.exe" for Windows
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

    public void Dispose()
    {
        _process.Kill();
        _process.Dispose();
    }
}
class Program
{
    static async Task Main(string[] args)
    {
        var server = new HttpListener();
        server.Prefixes.Add("http://localhost:8000/");
        Provider.SettingsProvider settings = new Provider.SettingsProvider();
        RunnableManager runnableManager = new RunnableManager();
        runnableManager.LoadRunnables();
        runnableManager.print();
        server.Start();
        Console.WriteLine("WebSocket server started at ws://localhost:8000/");
        // while (true)
        // {
        //     var context = await server.GetContextAsync();
        //     if (context.Request.IsWebSocketRequest)
        //     {
        //         var path = context.Request.Url.AbsolutePath;
        //         var wsContext = await context.AcceptWebSocketAsync(null);
                
        //         switch (path)
        //         {
        //             case "/code":
        //                 _ = HandleCodeWebSocket(wsContext.WebSocket);
        //                 break;
        //             case "/PMS":
        //                 _ = HandlePmsWebSocket(wsContext.WebSocket);
        //                 break;
        //             default:
        //                 Console.WriteLine($"Invalid endpoint: {path}");
        //                 // await wsContext.WebSocket.CloseAsync(
        //                 //     WebSocketCloseStatus.InvalidPayloadData,
        //                 //     "Invalid endpoint",
        //                 //     CancellationToken.None);
        //                 break;
        //         }
        //     }
        // }
    }

    // Rename the existing HandleWebSocketConnection to HandleCodeWebSocket
    static async Task HandleCodeWebSocket(WebSocket webSocket)
    {
        var receiveBuffer = new byte[2048];
        var receiveResult = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
        var message = Encoding.UTF8.GetString(receiveBuffer, 0, receiveResult.Count);
        var unpacked_json = JsonConvert.DeserializeObject<Dictionary<string, string>>(message);
        Provider.SettingsProvider settings = new Provider.SettingsProvider();
        Console.WriteLine($"Received code message: {message}");
        // use the regex to check if the code contains a comment with the comment being:
        // File_name: <filename>
        // Project: <project_name>
    }

    // New endpoint handler for /pms
    static async Task HandlePmsWebSocket(WebSocket webSocket)
    {
        // Implement PMS-specific logic here
        var receiveBuffer = new byte[1024];
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var receiveResult = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                if (receiveResult.MessageType == WebSocketMessageType.Text)
                {
                    // Handle PMS messages
                    var message = Encoding.UTF8.GetString(receiveBuffer, 0, receiveResult.Count);
                    var unpacked_json = JsonConvert.DeserializeObject<Dictionary<string, string>>(message);
                    Console.WriteLine($"Received PMS message: {message}");
                            // read the message as json and get the keys and values.
        /*
        example 
         {
        "PMS_System": "1.2.0",
    "Project_Name": "text",
    "Main_File": "main.c",
    "Project_Build_Systems": "cmake",
    "Project_Output": "main",
    "Run_On_Build": "True"
}
*/
                }
                else if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        string.Empty,
                        CancellationToken.None);
                }
            }
        }
        catch (Exception)
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.InternalServerError,
                string.Empty,
                CancellationToken.None);
        }
    }
}
