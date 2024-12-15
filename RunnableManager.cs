using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Tuvalu.logger;

namespace KodeRunner
{
    public class RunnableManager
    {
        private readonly Dictionary<
            string,
            List<(int Priority, Action<Provider.ISettingsProvider> Action)>
        > _runnables =
            new Dictionary<
                string,
                List<(int Priority, Action<Provider.ISettingsProvider> Action)>
            >();

        public void print()
        {
            foreach (var runnable in _runnables)
            {
                foreach (var (priority, action) in runnable.Value)
                {
                    var attr = action.Method.GetCustomAttribute<RunnableAttribute>();
                    Console.WriteLine(
                        $"Language: {runnable.Key}, Name: {action.Method.Name}, Priority: {priority}"
                    );
                }
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
                    var runnables = assembly
                        .GetTypes()
                        .Where(type =>
                            typeof(IRunnable).IsAssignableFrom(type)
                            && !type.IsInterface
                            && !type.IsAbstract
                        )
                        .Select(type => (IRunnable)Activator.CreateInstance(type))
                        .ToList();

                    foreach (var runnable in runnables)
                    {
                        var attribute = runnable.GetType().GetCustomAttribute<RunnableAttribute>();
                        if (attribute != null)
                        {
                            RegisterRunnable(
                                attribute.Language,
                                runnable.Execute,
                                attribute.Priority
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error loading assembly {dllPath}", ex);
                }
            }
        }

        public void LoadRunnables()
        {
            var runnables = AppDomain
                .CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type =>
                    typeof(IRunnable).IsAssignableFrom(type)
                    && !type.IsInterface
                    && !type.IsAbstract
                )
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
                    RegisterRunnable(attribute.Language, runnable.Execute, attribute.Priority);
                }
            }
        }

        public void ExecuteAll(Provider.ISettingsProvider settings)
        {
            foreach (var runnable in _runnables)
            {
                foreach (var (_, action) in runnable.Value)
                {
                    action(settings);
                }
            }
        }

        public void ExecuteFirstMatchingLanguage(
            string language,
            Provider.ISettingsProvider settings
        )
        {
            if (_runnables.TryGetValue(language, out var actions))
            {
                var highestPriorityAction = actions
                    .OrderByDescending(a => a.Priority)
                    .FirstOrDefault();
                highestPriorityAction.Action(settings);
                return;
            }
            throw new KeyNotFoundException($"No runnable found for language: {language}");
        }

        public void RegisterRunnable(
            string language,
            Action<Provider.ISettingsProvider> action,
            int priority = 0
        )
        {
            if (!_runnables.ContainsKey(language))
            {
                _runnables[language] =
                    new List<(int Priority, Action<Provider.ISettingsProvider> Action)>();
            }
            _runnables[language].Add((priority, action));
            Logger.Log($"Registered Runnable: Language: {language}, Priority: {priority}");
        }

        public void Execute(string key, Provider.ISettingsProvider settings)
        {
            if (_runnables.TryGetValue(key, out var actions))
            {
                foreach (var (_, action) in actions)
                {
                    action(settings);
                }
            }
            else
            {
                throw new KeyNotFoundException($"No runnable found for key: {key}");
            }
        }
    }
}
