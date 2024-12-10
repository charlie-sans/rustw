using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace KodeRunner
{
    public class RunnableManager
    {
        private readonly Dictionary<string, Action<Provider.ISettingsProvider>> _runnables = new Dictionary<string, Action<Provider.ISettingsProvider>>();

        public void print()
        {
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
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading assembly {dllPath}: {ex.Message}");
                }
            }
        } 

        public void LoadRunnables()
        {
            var runnables = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(IRunnable).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                .Select(type => (IRunnable)Activator.CreateInstance(type))
                .ToList();
            if (runnables.Count == 0)
            {
                // register the runables inside the directory
                LoadRunnablesFromDirectory("Runnables");
        
            }
            foreach (var runnable in runnables)
            {
                var attribute = runnable.GetType().GetCustomAttribute<RunnableAttribute>();
                if (attribute != null)
                {
                    RegisterRunnable(attribute.Name, runnable.Name, runnable.Execute, attribute.Priority);
                }
            }
        }

        public void ExecuteAll(Provider.ISettingsProvider settings)
        {
            foreach (var runnable in _runnables)
            {
                runnable.Value(settings);
            }
        }

        public void ExecuteFirstMatchingLanguage(string language, Provider.ISettingsProvider settings)
        {
            foreach (var runnable in _runnables)
            {
                if (runnable.Key.StartsWith(language))
                {
                    runnable.Value(settings);
                    return;
                }
            }
            throw new KeyNotFoundException($"No runnable found for language: {language}");
        }

        public void RegisterRunnable(string name, string language, Action<Provider.ISettingsProvider> action, int priority = 0, string description = null)
        {
            string key = $"{language}_{name}_{priority}";
            _runnables[key] = action;
        }

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
}