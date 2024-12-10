using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using KodeRunner;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;

namespace KodeRunner
{
    class Core
    {
        public static string GetVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
        public static string CodeDir = "Projects";
        public static string BuildDir = "Builds";
        public static string TempDir = "Temp";
        public static string OutputDir = "Output";
        public static string LogDir = "Logs";
        public static string ConfigDir = "Config";
        public static string ConfigFile = "config.json";
        public static string RootDir = Directory.GetCurrentDirectory() + "/koderunner";
    }
}