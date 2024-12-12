using dotenv.net;

namespace KodeRunner
{
    public class Envars
    {
        private static readonly DotEnvOptions options = new(
            probeForEnv: true,
            probeLevelsToSearch: 2
        );
        private static readonly IDictionary<string, string> vars = DotEnv.Read(options);
        private static readonly IDictionary<string, string> dotenv = DotEnv.Read();
        public string ServerURL = vars.TryGetValue("ServerURL", out var serverURL)
            ? serverURL
            : "http://localhost:5000/";
        public string ServerPort = vars.TryGetValue("ServerPort", out var serverPort)
            ? serverPort
            : string.Empty;
        public string ServerPath = vars.TryGetValue("ServerPath", out var serverPath)
            ? serverPath
            : string.Empty;
        public string HTMLRoot = dotenv.TryGetValue("HTMLRoot", out var htmlRoot)
            ? htmlRoot
            : string.Empty;
    }
}
