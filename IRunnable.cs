using System;
using System.Collections.Generic;
using System.Reflection;

namespace KodeRunner
{
    #region Interfaces
    /// <summary>
    ///  Interface for all runnables
    /// implements Execute method, Name, Language, Priority and description properties.
    /// if a class implements this interface, it can be registered as a runnable that can be used to run programs in the specified language set in the Language property.
    /// 
    /// </summary>
    public interface IRunnable
    {
        void Execute(Provider.ISettingsProvider settings);
        string Name { get; }
        string Language { get; }
        int Priority { get; }
        string description { get; }
    }
    #endregion

    #region Attributes
    /// <summary>
    ///  Runnable attribute to be used on classes that implement IRunnable for registering runnables that run Programs in the specified language
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    [AttributeUsage(AttributeTargets.Class)]
    public class RunnableAttribute : Attribute
    {
        public string Name { get; }
        public string Language { get; }
        public int Priority { get; }

        public RunnableAttribute(string name, string language, int priority = 0)
        {
            Name = name;
            Language = language;
            Priority = priority;
        }
    }
    #endregion

    #region Core Classes
    public class RunnableManager
    {
        /// <summary>
        ///  Dictionary of all runnables
        /// </summary>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="TypeLoadException"></exception>
        /// <exception cref="MissingMethodException"></exception>
        /// <exception cref="MemberAccessException"></exception>
        private readonly Dictionary<string, Action<Provider.ISettingsProvider>> _runnables = new Dictionary<string, Action<Provider.ISettingsProvider>>();
        /// <summary>
        ///  Prints all runnables to the console for debugging
        /// </summary>
        /// 
        /// 
        

         
        public void print()
        {
            // loop over the dictionary and print the language, name and priority of each runnable

            foreach (var runnable in _runnables)
            {
                var attr = runnable.Value.Method.GetCustomAttribute<RunnableAttribute>();
                Console.WriteLine($"Language: {runnable.Key}, Name: {runnable.Value.Method.Name}, Priority: {attr?.Priority ?? 0}");
            }
        }

        public void LoadRunnablesFromDirectory(string path)
        {
            var dllFiles = Directory.GetFiles(path, "*.dll");
            foreach (var dllPath in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dllPath);
                    var runnables = assembly.GetTypes()
                        .Where(type => typeof(IRunnable).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        .Select(static type => (IRunnable)Activator.CreateInstance(type))
                        .ToList();

                    foreach (var runnable in runnables)
                    {
                        var attribute = runnable.GetType().GetCustomAttribute<RunnableAttribute>();
                        if (attribute != null)
                        {
                            RegisterRunnable(attribute.Name, runnable.Name, runnable.Execute, attribute.Priority);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading assembly {dllPath}: {ex.Message}");
                }
            }
        }


        /// <summary>
        ///  Loads all runnables from the current app domain
        /// </summary>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="TypeLoadException"></exception>
        /// <exception cref="MissingMethodException"></exception>
        /// <exception cref="MemberAccessException"></exception>
        /// <exception cref="TargetInvocationException"></exception>
        public void LoadRunnables()
        {
            var runnables = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(IRunnable).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                .Select(type => (IRunnable)Activator.CreateInstance(type))
                .ToList();

            foreach (var runnable in runnables)
            {
                var attribute = runnable.GetType().GetCustomAttribute<RunnableAttribute>();
                if (attribute != null)
                {
                    RegisterRunnable(attribute.Name, runnable.Name, runnable.Execute, attribute.Priority);
                }
            }
        }
        /// <summary>
        ///  Executes all runnables
        /// </summary>
        /// <param name="settings"></param>
        public void ExecuteAll(Provider.ISettingsProvider settings)
        {
            foreach (var runnable in _runnables)
            {
                runnable.Value(settings);
            }
        }
        /// <summary>
        ///     Executes the first matching language
        ///    Throws a KeyNotFoundException if no runnable is found for the specified language 
        /// </summary>

        /// <param name="language"></param>
        /// <param name="settings"></param>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="TypeLoadException"></exception>
        public void ExecuteFirstMatchingLanguage(string language, Provider.ISettingsProvider settings)
        {
            var matchingAction = _runnables.FirstOrDefault(r => r.Key.Equals(language, StringComparison.OrdinalIgnoreCase));

            if (!matchingAction.Equals(default(KeyValuePair<string, Action<Provider.ISettingsProvider>>)))
            {
                matchingAction.Value(settings);
            }
            else
            {
                throw new KeyNotFoundException($"No runnable found for language: {language}");
            }
        }
        /// <summary>
        ///  Registers a runnable with the specified name, language, action, priority and description
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="language"></param>
        /// <param name="action"></param>
        /// <param name="priority"></param>
        /// <param name="description"></param>
        public void RegisterRunnable(string name, string language, Action<Provider.ISettingsProvider> action, int priority = 0, string description = null)
        {
            string key = $"{language}_{name}_{priority}";
            _runnables[key] = action;
        }
        /// <summary>
        ///  Executes the runnable with the specified key. 
        /// Throws a KeyNotFoundException if no runnable is found for the specified key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="settings"></param>
        public void Execute(string key, Provider.ISettingsProvider settings)
        {
            if (_runnables.TryGetValue(key, out var action))
            {
                action(settings);
            }
            else
            {
                throw new KeyNotFoundException($"No runnable found for key: {key}");
            }
        }
    }
    #endregion

    #region Implementations
    /// <summary>
    /// template testing classes for dotnet and python runnables.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
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
            Console.WriteLine("Running dotnet with metadata");
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
        }
    }



    #endregion

    #region Settings
    /// <summary>
    ///  Settings provider for the runnable manager
    /// 
    /// </summary>
    public class Provider
    {
        /// <summary>
        ///  Interface for the settings provider
        ///  Contains all the settings that can be used to configure the runnable manager or said runner classes.
        /// when you implement this interface, you can use the settings to configure the runnable manager or the runner classes for use with the runnable manager and execute the runnables.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public interface ISettingsProvider
        {
            string Language { get; set; }
            string Framework { get; set; }
            string ProjectType { get; set; }
            string ProjectName { get; set; }
            string ProjectPath { get; set; }
            bool IsWebProject { get; set; }
            bool Run_On_Build { get; set; }
            string Main_File { get; set; }
            string BuildCommand { get; set; }
            string RunCommand { get; set; }
            string TestCommand { get; set; }
            string[] BuildArgs { get; set; }
            string[] RunArgs { get; set; }
            string[] TestArgs { get; set; }
            string[] BuildFlags { get; set; }
            string[] RunFlags { get; set; }
            string[] TestFlags { get; set; }
            string[] BuildOptions { get; set; }
            string[] RunOptions { get; set; }
            string[] TestOptions { get; set; }
            string[] BuildEnv { get; set; }
            string[] RunEnv { get; set; }
        }

        public struct SettingsProvider : ISettingsProvider
        {
            public string Language { get; set; }
            public string Framework { get; set; }
            public string ProjectType { get; set; }
            public string ProjectName { get; set; }
            public string ProjectPath { get; set; }
            public bool IsWebProject { get; set; }
            public bool Run_On_Build { get; set; }
            public string Main_File { get; set; }
            public string BuildCommand { get; set; }
            public string RunCommand { get; set; }
            public string TestCommand { get; set; }
            public string[] BuildArgs { get; set; }
            public string[] RunArgs { get; set; }
            public string[] TestArgs { get; set; }
            public string[] BuildFlags { get; set; }
            public string[] RunFlags { get; set; }
            public string[] TestFlags { get; set; }
            public string[] BuildOptions { get; set; }
            public string[] RunOptions { get; set; }
            public string[] TestOptions { get; set; }
            public string[] BuildEnv { get; set; }
            public string[] RunEnv { get; set; }
        }
    }
    #endregion
}
