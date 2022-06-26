using HostLibrary.Core.Structs;
using HostLibrary.StaticClasses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace HostLibrary.Core
{
    /// <summary>
    /// Граф модулей
    /// </summary>
    public class ModulesGraph : IEnumerable<InternalModule>
    {
        #region Interface realization

        public IEnumerator<InternalModule> GetEnumerator() => _nodes.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region Fields

        public readonly List<Edge> _edges = new List<Edge>();

        private readonly List<InternalModule> _nodes = new List<InternalModule>();

        private readonly Dictionary<string, List<string>> _missingDependencies = new Dictionary<string, List<string>>();

        private readonly List<Node> _roots = new List<Node>();

        /// <summary>
        /// Корневые модули, модули без зависимостей
        /// </summary>
        public IEnumerable<Node> Roots => _roots;

        /// <summary>
        /// Список всех зависимостей модулей ввиде линеного списка
        /// </summary>
        public IEnumerable<Edge> Edges => _edges;

        /// <summary>
        /// Список не разрешённых зависимостей и модулей их требующие
        /// </summary>
        public IReadOnlyDictionary<string, List<string>> MissingDependencies => _missingDependencies;

        #endregion

        /// <summary>
        /// Получает список, отсортированный по зависимостям
        /// </summary>
        public static IEnumerable<ModuleMetadata> GetOrderedSources(IEnumerable<ModuleMetadata> source)
        {
            Dictionary<ModuleMetadata, int> orderedSource = new();

            foreach (var md in source)
            {
                orderedSource.Add(md, 0);
            }

            void checkForCycleDep(ModuleMetadata moduleForCheck, ModuleMetadata nextModule)
            {
                if (nextModule.Dependencies?.Any(p => p.Key == moduleForCheck.Name) ?? false)
                {
                    List<string> errorList = new();
                    errorList.Add(moduleForCheck.Name);
                    errorList.AddRange(nextModule.Dependencies.Select(p => p.Key));
                    errorList.Reverse();
                    StartupLogger.LogInformation(InternalLocalizers.General["CYCLE_REFERENCE", string.Join("\", \"", errorList)]);
                    throw new ApplicationException(string.Join("\", \"", errorList));
                }

                var list = source.Where(m => nextModule.Dependencies?.Any(d => d.Key == m.Name) ?? false);

                if (list != null && list.Any())
                {
                    foreach (var item in list)
                    {
                        checkForCycleDep(moduleForCheck, item);
                    }
                }
            }

            void calculateWeight(ModuleMetadata moduleMetadata, Dictionary<ModuleMetadata, int> list)
            {
                var mdDeps = list
                    .Where(p => p.Key.Dependencies?
                        .Any(d => d.Key == moduleMetadata.Name) ?? false)
                    .ToList();

                foreach (var mdDep in mdDeps)
                {
                    list[mdDep.Key]++;
                    calculateWeight(mdDep.Key, list);
                }
            }

            foreach (var md in orderedSource)
            {
                checkForCycleDep(md.Key, md.Key);
                calculateWeight(md.Key, orderedSource);
            }

            return orderedSource
                .OrderBy(p => p.Value)
                .ThenBy(p => p.Key.Name)
                .Select(p => p.Key);
        }

        /// <summary>
        /// Граф модулей
        /// </summary>
        public ModulesGraph(IEnumerable<ModuleMetadata> source)
        {
            var sourceList = source.ToList();
            foreach (var md in GetOrderedSources(sourceList))
            {
                var mod = new InternalModule(md);

                _missingDependencies.Remove(md.Key);

                _nodes.Add(mod);

                if ((md.Dependencies?.Count ?? 0) == 0)
                {
                    _roots.Add(new Node(this, mod));
                    continue;
                }

                foreach (var dep in md.Dependencies!)
                {
                    var key = $"{dep.Key}:{dep.Value}";
                    _edges.Add(new Edge
                    {
                        From = key,
                        To = md.Key
                    });

                    if (!_nodes.Any(n => n.Metadata.Key == key))
                    {
                        if (!_missingDependencies.TryGetValue(key, out var list))
                            _missingDependencies.Add(key, new List<string>(new[] { md.Name }));
                        else
                            _missingDependencies[key].Add(md.Name);
                    }
                }
            }
        }

        /// <summary>
        /// Проход по узлам графа и выполнение действия
        /// </summary>
        /// <remarks>Действие для модуля с зависимостью будет выполнено только после всех модулей от он зависит</remarks>
        /// <param name="action">Действие выполняемое для каждого узла</param>
        public void TraverseAndExecute(Action<Node> action)
        {
            Queue<Node> nodes = new Queue<Node>(Roots);
            List<string> loaded = new List<string>();

            while (nodes.Count > 0)
            {
                var node = nodes.Dequeue();

                var deps = node.Module.Metadata.Dependencies?.Select(s => s.Key);
                if (deps?.Any() == true)
                {
                    var skip = false;
                    foreach (var dep in deps)
                    {
                        if (!loaded.Contains(dep))
                        {
                            nodes.Enqueue(node);
                            skip = true;
                            break;
                        }
                    }

                    if (skip)
                        continue;
                }

                action(node);
                loaded.Add(node.Module.Name);

                if (node.DependantNodes?.Any() == true)
                {
                    foreach (var depNode in node.DependantNodes)
                    {
                        if (!nodes.Any(n => n.Module.Name == depNode.Module.Name))
                            nodes.Enqueue(depNode);
                    }
                }
            }
        }
    }
}