using System;
using System.IO;
using System.Reflection;

namespace KodeRunner
{
    public class Core
    {
        public static string GetVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
        public static string RootDir = Directory.GetCurrentDirectory();
        public static string CodeDir = "koderunner/Projects";
        public static string BuildDir = "Builds";
        public static string TempDir = "Temp";
        public static string OutputDir = "Output";
        public static string LogDir = "Logs";
        public static string ConfigDir = "Config";
        public static string ConfigFile = "config.json";
        public static string ConfigPath = Directory.GetCurrentDirectory() + "/" + ConfigDir + "/" + ConfigFile;
        // locatiion inside the root dir for adding custom runnnables
        public static string RunnableDir = Path.Combine(RootDir, "Runnables");
    }
}