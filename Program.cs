using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using dotenv.net;
using KodeRunner;
using Newtonsoft.Json;
using Tuvalu.logger;

namespace KodeRunner
{
    class Program
    {
        static Dictionary<string, WebSocket> activeConnections =
            new Dictionary<string, WebSocket>();
        static TerminalProcess terminalProcess = new TerminalProcess();

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
            @".*\*/$",
        };

        public static string PMS_VERSION = "1.2.2";

        // Add the RunnableManager as a static field
        static RunnableManager runnableManager = new RunnableManager();
        static Envars Envars = new Envars();

        public static void NotifySubscribedUsers(string message)
        {
            foreach (var connection in activeConnections.Values)
            {
                if (connection.State == WebSocketState.Open)
                {
                    _ = SendWebsocketMessage(connection, message);
                }
            }
        }

        static async Task Main(string[] args)
        {
            Logger.Log("Starting KodeRunner...");
            var server = new HttpListener();
            DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: false));
            //Console.WriteLine($"Server URL: {Envars.ServerURL}");
            server.Prefixes.Add(Envars.ServerURL);

            Provider.SettingsProvider settings = new Provider.SettingsProvider();
            // Remove the local declaration of runnableManager
            // RunnableManager runnableManager = new RunnableManager();
            runnableManager.LoadRunnables();

            // check the runnables dir for any dlls
            if (Directory.Exists(Core.RunnableDir))
            {
                // if there any dlls in the directory, load them
                if (Directory.GetFiles(Core.RunnableDir, "*.dll").Length > 0)
                {
                    runnableManager.LoadRunnablesFromDirectory(Core.RunnableDir);
                }
            }

            server.Start();
            Logger.Log("Server started.");

            Console.WriteLine($"KodeRunner v{Core.GetVersion()} started");
            Console.WriteLine($"PMS v{PMS_VERSION} started");

            BuildProcess buildProcess = new BuildProcess();
            buildProcess.SetupCodeDir();

            Console.WriteLine("WebSocket server started at " + Envars.ServerURL);
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
                            Logger.Log(
                                "WebSocket connection to /code endpoint from IP: "
                                    + context.Request.RemoteEndPoint.Address
                            );
                            _ = HandleCodeWebSocket(wsContext.WebSocket);
                            break;
                        case "/PMS":
                            Logger.Log(
                                "WebSocket connection to /PMS endpoint from IP: "
                                    + context.Request.RemoteEndPoint.Address
                            );
                            _ = HandlePmsWebSocket(wsContext.WebSocket);
                            break;
                        case "/stop":
                            Logger.Log(
                                "WebSocket connection to /stop endpoint from IP: "
                                    + context.Request.RemoteEndPoint.Address
                            );
                            _ = HandleStopWebSocket(wsContext.WebSocket);
                            break;
                        case "/terminput":
                            Logger.Log(
                                "WebSocket connection to /terminput endpoint from IP: "
                                    + context.Request.RemoteEndPoint.Address
                            );
                            _ = HandleTerminalInput(wsContext.WebSocket);
                            break;
                        default:
                            Logger.Log($"Invalid endpoint: {path}");
                            break;
                    }
                }
                else
                {
                    await HandleHttpRequest(context);
                }
            }
        }

        public static async Task SendWebsocketMessage(WebSocket webSocket, string message)
        {
            Logger.Log($"Sending WebSocket message: {message}");
            var bytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }

        static async Task HandleHttpRequest(HttpListenerContext context)
        {
            Logger.Log("Handling HTTP request...");
            try
            {
                string localPath = context.Request.Url?.LocalPath ?? "/";
                if (localPath == "/")
                {
                    localPath = "/index.html";
                }

                string filePath = Path.Combine(Core.RootDir, "wwwroot", localPath.TrimStart('/'));
                if (File.Exists(filePath))
                {
                    byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                    context.Response.ContentType = GetContentType(filePath);
                    context.Response.ContentLength64 = fileBytes.Length;
                    await context.Response.OutputStream.WriteAsync(
                        fileBytes.AsMemory(),
                        CancellationToken.None
                    );
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    byte[] errorBytes = Encoding.UTF8.GetBytes("404 - File Not Found");
                    await context.Response.OutputStream.WriteAsync(
                        errorBytes.AsMemory(),
                        CancellationToken.None
                    );
                }
            }
            catch (HttpListenerException)
            {
                // Client disconnected, ignore the error
                Logger.Log("Client disconnected.");
            }
            catch (Exception ex)
            {
                Logger.Log("Error handling HTTP request", ex);
            }
            finally
            {
                try
                {
                    context.Response.Close();
                }
                catch
                {
                    // Ignore any errors during response close
                }
            }
        }

        static string GetContentType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".gif" => "image/gif",
                _ => "application/octet-stream",
            };
        }

        static async Task HandleTerminalInput(WebSocket webSocket)
        {
            Logger.Log("Terminal input endpoint connected");
            terminalProcess.OnOutput += async (output) =>
                await SendWebsocketMessage(webSocket, output);
            try
            {
                activeConnections["terminput"] = webSocket;
                var buffer = new byte[8192];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "",
                            CancellationToken.None
                        );
                        _ = activeConnections.Remove("terminput");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await memoryStream.WriteAsync(buffer, 0, result.Count);
                            _ = memoryStream.Seek(0, SeekOrigin.Begin);
                            var message = await ReadFromMemoryStream(memoryStream);
                            Console.WriteLine($"Received message: {message}");
                            Console.WriteLine(terminalProcess.SendInput(message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("WebSocket error", ex);
                _ = activeConnections.Remove("terminput");
                runnableManager.LoadRunnables();
            }
        }

        static async Task HandleCodeWebSocket(WebSocket webSocket)
        {
            Logger.Log("Code endpoint connected");
            terminalProcess.OnOutput += async (output) =>
                await SendWebsocketMessage(webSocket, output);
            var projectnamefound = false;
            var filenamefound = false;
            try
            {
                activeConnections["code"] = webSocket;
                var buffer = new byte[8192];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "",
                            CancellationToken.None
                        );
                        _ = activeConnections.Remove("code");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await memoryStream.WriteAsync(buffer, 0, result.Count);
                            _ = memoryStream.Seek(0, SeekOrigin.Begin);
                            var message = await ReadFromMemoryStream(memoryStream);
                            var lines = message.Split('\n');
                            if (lines.Length == 0)
                                continue;
                            //Console.WriteLine($"Received message: {message}");
                            var commentLines = lines
                                .Where(line =>
                                    CommentRegexes.Any(regex => Regex.IsMatch(line, regex))
                                )
                                .ToList();
                            var commentContent = new StringBuilder();
                            bool inBlockComment = false;

                            foreach (var line in lines)
                            {
                                if (Regex.IsMatch(line, @"^/\*.*\*/$")) // Single line block comment
                                {
                                    _ = commentContent.AppendLine(line);
                                }
                                else if (Regex.IsMatch(line, @"^/\*.*$")) // Start of block comment
                                {
                                    _ = commentContent.AppendLine(line);
                                    inBlockComment = true;
                                }
                                else if (Regex.IsMatch(line, @".*\*/$")) // End of block comment
                                {
                                    _ = commentContent.AppendLine(line);
                                    inBlockComment = false;
                                }
                                else if (
                                    inBlockComment
                                    || CommentRegexes.Any(regex => Regex.IsMatch(line, regex))
                                )
                                {
                                    _ = commentContent.AppendLine(line);
                                }
                                else
                                {
                                    _ = commentContent.AppendLine(line);
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
                                Console.WriteLine(
                                    $"Project name: {projectNameMatch.Groups[1].Value}"
                                );
                                projectnamefound = true;
                            }
                            if (projectnamefound && filenamefound)
                            {
                                // Trim project and file names to remove any extra whitespace
                                string projectName = projectNameMatch.Groups[1].Value.Trim();
                                string fileName = fileNameMatch.Groups[1].Value.Trim();

                                // Use Path.Combine to construct paths
                                string project_path = Path.Combine(
                                    Core.RootDir,
                                    Core.CodeDir,
                                    projectName
                                );
                                string file_path = Path.Combine(project_path, fileName);

                                if (!Directory.Exists(project_path))
                                {
                                    _ = Directory.CreateDirectory(project_path);
                                }

                                File.WriteAllText(file_path, message);
                                Console.WriteLine($"File saved to {file_path}");
                                _ = SendWebsocketMessage(
                                    webSocket,
                                    $"File: {fileName} saved to {project_path}"
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("WebSocket error", ex);
                runnableManager.LoadRunnables();
                _ = activeConnections.Remove("code");
            }
        }

        public static async Task<string> ReadFromMemoryStream(MemoryStream memoryStream)
        {
            _ = memoryStream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public static async Task SendToWebSocket(string endpoint, string message)
        {
            if (
                activeConnections.TryGetValue(endpoint, out WebSocket socket)
                && socket.State == WebSocketState.Open
            )
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        }

        static async Task HandlePmsWebSocket(WebSocket webSocket)
        {
            Console.WriteLine("PMS endpoint connected");
            // setup memory stream like in HandleCodeWebSocket
            var ProjectName = "";
            var FileName = "";
            var Main_File = "";
            var Project_Build_Systems = "";
            var Project_Output = "";
            var Run_On_Build = false;

            /*
            template for the json message
             {
            "PMS_System": "1.2.0",
        "Project_Name": "text",
        "Main_File": "main.c",
        "Project_Build_Systems": "cmake",
        "Project_Output": "main",
        "Run_On_Build": "True"
    }
            */

            try
            {
                activeConnections["PMS"] = webSocket;
                var buffer = new byte[8192];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "",
                            CancellationToken.None
                        );
                        activeConnections.Remove("PMS");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await memoryStream.WriteAsync(buffer, 0, result.Count);
                            memoryStream.Seek(0, SeekOrigin.Begin);
                            var message = await ReadFromMemoryStream(memoryStream);
                            Console.WriteLine($"Received message: {message}");
                            // we now have the json message in the message variable
                            // we can now parse it into a dictionary
                            var messageDict = JsonConvert.DeserializeObject<
                                Dictionary<string, string>
                            >(message);
                            // we can now access the values in the dictionary
                            if (messageDict.TryGetValue("Project_Name", out string project))
                            {
                                Console.WriteLine($"Project: {project}");
                                ProjectName = project.Trim();
                            }
                            if (messageDict.TryGetValue("Main_File", out string mainfile))
                            {
                                Console.WriteLine($"Main File: {mainfile}");
                                Main_File = mainfile;
                            }
                            if (
                                messageDict.TryGetValue(
                                    "Project_Build_Systems",
                                    out string buildsystems
                                )
                            )
                            {
                                Console.WriteLine($"Build Systems: {buildsystems}");
                                Project_Build_Systems = buildsystems;
                            }
                            if (messageDict.TryGetValue("Project_Output", out string output))
                            {
                                Console.WriteLine($"Project Output: {output}");
                                Project_Output = output;
                            }
                            if (messageDict.TryGetValue("Run_On_Build", out string runonbuild))
                            {
                                Console.WriteLine($"Run On Build: {runonbuild}");
                                Run_On_Build = runonbuild == "True";
                            }

                            // Ensure parent directories exist
                            string project_path = Path.Combine(
                                Core.RootDir,
                                Core.CodeDir,
                                ProjectName
                            );
                            string file_path = Path.Combine(project_path, Core.ConfigFile);

                            // if (!Directory.Exists(project_path))
                            // {
                            //     Directory.CreateDirectory(project_path);
                            // }

                            //File.WriteAllText(file_path, message);

                            // we can now build the project using the IRunnableManager
                            // we can use the project name to get the project directory, and the main file to build the project
                            // we can use the build systems to determine how to build the project

                            // look for the matching runnable
                            Provider.ISettingsProvider settings = new Provider.SettingsProvider();
                            settings.Main_File = Main_File;
                            settings.ProjectName = ProjectName;
                            settings.Run_On_Build = Run_On_Build;
                            settings.Language = Project_Build_Systems;
                            settings.Output = Project_Output;
                            settings.PmsWebSocket = webSocket; // Set the PMS WebSocket
                            // Set the ProjectPath
                            settings.ProjectPath = Path.Combine(
                                Core.RootDir,
                                Core.CodeDir,
                                ProjectName
                            );
                            // Use the existing runnableManager instance
                            runnableManager.ExecuteFirstMatchingLanguage(
                                Project_Build_Systems,
                                settings
                            );
                            runnableManager.print();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
                activeConnections.Remove("PMS");
                runnableManager.LoadRunnables();
            }
        }

        static async Task HandleStopWebSocket(WebSocket webSocket)
        {
            Logger.Log("Stop endpoint connected");
            terminalProcess.OnOutput += async (output) =>
                await SendWebsocketMessage(webSocket, output);
            try
            {
                activeConnections["stop"] = webSocket;
                var buffer = new byte[1024];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "",
                            CancellationToken.None
                        );
                        _ = activeConnections.Remove("stop");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        await using (var memoryStream = new MemoryStream())
                        {
                            await memoryStream.WriteAsync(
                                buffer.AsMemory(0, result.Count),
                                CancellationToken.None
                            );
                            var message = await ReadFromMemoryStream(memoryStream);
                            var messageDict = JsonConvert.DeserializeObject<
                                Dictionary<string, bool>
                            >(message);

                            if (
                                messageDict != null
                                && messageDict.TryGetValue("stopped", out bool shouldStop)
                                && shouldStop
                            )
                            {
                                Console.WriteLine("Stopping all processes...");
                                TerminalProcess.StopAllProcesses();

                                // Send confirmation back to client
                                var response = JsonConvert.SerializeObject(
                                    new { stopped = true, message = "All processes stopped" }
                                );
                                var responseBytes = Encoding.UTF8.GetBytes(response);
                                await webSocket.SendAsync(
                                    new ArraySegment<byte>(responseBytes),
                                    WebSocketMessageType.Text,
                                    true,
                                    CancellationToken.None
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("WebSocket error", ex);
                runnableManager.LoadRunnables();
                _ = activeConnections.Remove("stop");
            }
        }
    }
}
