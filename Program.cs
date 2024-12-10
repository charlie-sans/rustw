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
    static Dictionary<string, WebSocket> activeConnections = new Dictionary<string, WebSocket>();
    public static List<string> CommentRegexes = new List<string>
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
        @"^#.*$",
        @"^//.*$",
        @"^/\*.*\*/$",
        @"^<!--.*-->$",
        @"^\"".*\""$",
        @"^//.*$",
        @"^<!--.*-->$",
        // start of block comment
        @"^/\*.*$",
        // end of block comment
        @".*\*/$"
    };

    public static string PMS_VERSION = "1.2.2";

    static async Task Main(string[] args)
    {
        var server = new HttpListener();
        server.Prefixes.Add("http://localhost:8000/");

        Provider.SettingsProvider settings = new Provider.SettingsProvider();
        RunnableManager runnableManager = new RunnableManager();
        runnableManager.LoadRunnables();
        runnableManager.print();
        server.Start();

        Console.WriteLine($"KodeRunner v{Core.GetVersion()} started");  
        Console.WriteLine($"PMS v{PMS_VERSION} started");

        BuildProcess buildProcess = new BuildProcess();
        buildProcess.SetupCodeDir();

        Console.WriteLine("WebSocket server started at ws://localhost:8000/");
        while (true)
        {
            var context = await server.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                var path = context.Request.Url.AbsolutePath;
                var wsContext = await context.AcceptWebSocketAsync(null);

                switch (path)
                {
                    case "/code":
                        _ = HandleCodeWebSocket(wsContext.WebSocket);
                        break;
                    case "/PMS":
                        _ = HandlePmsWebSocket(wsContext.WebSocket);
                        break;
                    default:
                        Console.WriteLine($"Invalid endpoint: {path}");
                        break;
                }
            }
        }
    }


    static async Task HandleCodeWebSocket(WebSocket webSocket)
    {
        Console.WriteLine("Code endpoint connected");
        var projectnamefound = false;
        var filenamefound = false;
        try
        {
            activeConnections["code"] = webSocket;
            var buffer = new byte[8192];

            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    activeConnections.Remove("code");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await memoryStream.WriteAsync(buffer, 0, result.Count);
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        var message = await ReadFromMemoryStream(memoryStream);
                        var lines = message.Split('\n');
                        if (lines.Length == 0) continue;
                        //Console.WriteLine($"Received message: {message}");
                        var commentLines = lines.Where(line => CommentRegexes.Any(regex => Regex.IsMatch(line, regex))).ToList();
                        var commentContent = new StringBuilder();
                        bool inBlockComment = false;

                        foreach (var line in lines)
                        {
                            if (Regex.IsMatch(line, @"^/\*.*\*/$")) // Single line block comment
                            {
                                commentContent.AppendLine(line);
                            }
                            else if (Regex.IsMatch(line, @"^/\*.*$")) // Start of block comment
                            {
                                commentContent.AppendLine(line);
                                inBlockComment = true;
                            }
                            else if (Regex.IsMatch(line, @".*\*/$")) // End of block comment
                            {
                                commentContent.AppendLine(line);
                                inBlockComment = false;
                            }
                            else if (inBlockComment || CommentRegexes.Any(regex => Regex.IsMatch(line, regex)))
                            {
                                commentContent.AppendLine(line);
                            }
                            else
                            {
                                commentContent.AppendLine(line);
                            }
                        }

                        var commentText = commentContent.ToString();
                        var fileNameMatch = Regex.Match(commentText, @"File_name\s*:\s*(.*)");
                        var projectNameMatch = Regex.Match(commentText, @"Project\s*:\s*(.*)");

                        if (fileNameMatch.Success)
                        {
                            Console.WriteLine($"File name: {fileNameMatch.Groups[1].Value}");
                            filenamefound = true;
                        }

                        if (projectNameMatch.Success)
                        {
                            Console.WriteLine($"Project name: {projectNameMatch.Groups[1].Value}");
                            projectnamefound = true;
                        }
                        if (projectnamefound && filenamefound)
                        {
                            using (StreamWriter sw = new StreamWriter("test.c"))
                            {
                                sw.WriteLine(commentText);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket error: {ex.Message}");
            activeConnections.Remove("code");
        }
    }


    public static async Task<string> ReadFromMemoryStream(MemoryStream memoryStream)
    {
        memoryStream.Seek(0, SeekOrigin.Begin);
        using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
        {
            return await reader.ReadToEndAsync();
        }
    }

    public static async Task SendToWebSocket(string endpoint, string message)
    {
        if (activeConnections.TryGetValue(endpoint, out WebSocket socket) && socket.State == WebSocketState.Open)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    static async Task HandlePmsWebSocket(WebSocket webSocket)
    {
        var receiveBuffer = new byte[1024];
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                if (receiveResult.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(receiveBuffer, 0, receiveResult.Count);
                    var unpacked_json = JsonConvert.DeserializeObject<Dictionary<string, string>>(message);
                    Console.WriteLine($"Received PMS message: {message}");
                }
                else if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
            }
        }
        catch (Exception)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, string.Empty, CancellationToken.None);
        }
    }
}
