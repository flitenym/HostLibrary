using HostLibrary.Core;
using HostLibrary.Core.Classes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Text.Json;

namespace HostLibrary.StaticClasses
{
    public static class StartupManager
    {
        private static ModulesGraph _graph;

        private const string HOST_NAME = "Хост системы";

        public const string UNINSTALL_PROMPT = "prompt";

        public static ModulesGraph Graph => _graph!;

        public static HostingOptions Options { get; private set; }

        public static string SettingsPath { get; set; }

        /// <summary>
        /// Список проверенных сборок
        /// </summary>
        public static List<(string Module, string Name, string FullName)> LoadedAssemblies { get; } = new List<(string, string, string)>();
        /// <summary>
        /// Информация о заменённых сборках
        /// </summary>
        public static List<AssemblyReplacementInfo> ReplacesAssemblies { get; } = new List<AssemblyReplacementInfo>();

        private static (T Value, int Index) FindWithIndex<T>(this IEnumerable<T> source, Predicate<T> match) where T : struct
        {
            var i = 0;
            foreach (var a in source)
            {
                if (match(a))
                    return (a, i);

                i++;
            }

            return (default, -1);
        }

        private static Assembly[] _hostAssemblies;
        public static Assembly[] HostAssemblies
        {
            get
            {
                if (_hostAssemblies == null)
                    _hostAssemblies = AssemblyLoadContext.Default.Assemblies.ToArray();
                return _hostAssemblies;
            }
        }

        /// <summary>
        /// Загрузка модулей и предварительная проверка
        /// </summary>
        /// <exception cref="MetadataValidationException">Если отсутствуют название или версия модуля, или название содежржит запрещённые символы</exception>
        /// <exception cref="ModuleSelfReferenceException">Если в метаданных присутствует ссылка на самого себя</exception>
        private static IEnumerable<ModuleMetadata> Metadata(HostingOptions options)
        {
            var order = 0;
            Dictionary<string, string> loaded = new Dictionary<string, string>();
            foreach (string file in Directory.EnumerateFiles(options.Configurations!, "*.json"))
            {
                StartupLogger.LogInformation(InternalLocalizers.General["METADATA_LOAD_START", file]);

                var md = JsonSerializer.Deserialize<ModuleMetadata>(File.ReadAllBytes(file))!;
                if (string.IsNullOrWhiteSpace(md.ModulePath))
                    md.ModulePath = Path.GetFullPath(Path.Combine(options.Installations!, md.Name));
                else if (!Path.IsPathRooted(md.ModulePath))
                    md.ModulePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, md.ModulePath));

                md.Validate();
                md.CheckModulePath();

                if (loaded.ContainsKey(md.Name))
                {
                    var path = loaded[md.Name];
                    throw new ApplicationException(string.Join("; ", md.Name, path, file));
                }
                else
                    loaded.Add(md.Name, file);

                StartupLogger.LogInformation(InternalLocalizers.General["METADATA_LOAD_END"]);
                md.Order = order;
                order++;

                yield return md;
            }
        }

        private static void AddConfiguration(IConfigurationBuilder configBuilder, InternalModule module, HostingOptions options)
        {
            if (module.Metadata.ExtraSettingsFiles?.Length > 0)
            {
                for (var i = 0; i < module.Metadata.ExtraSettingsFiles.Length; i++)
                {
                    var setPath = module.Metadata.ExtraSettingsFiles[i];
                    if (!Path.IsPathRooted(setPath))
                        module.Metadata.ExtraSettingsFiles[i] = setPath = module.GetFilePath(setPath);
                    configBuilder.AddJsonFile(setPath, true, true);
                }
            }
        }

        private static void EnsureAssemblyVersion(AssemblyInfo asm, string filePath)
        {
            if (asm.AssemblyVersion == null)
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var peReader = new PEReader(fs);

                MetadataReader mr = peReader.GetMetadataReader();

                var version = mr.GetAssemblyDefinition().Version;

                if (version != null)
                    asm.AssemblyVersion = version;
            }
        }

        /// <summary>
        /// Загрузка дополнительных сборок модуля
        /// </summary>
        /// <param name="module">Информация о модуле</param>
        /// <param name="assemblies">Список сборок которые надо загузить в контекст после проверки версий</param>
        /// <exception cref="AssemblyVersionIncompatibleException">Вызывается если ProductVersion хоста ниже ProductVersion модуля</exception>
        private static void LoadExtraAssemblies(InternalModule module, List<(AssemblyInfo Assembly, string Path, string ModuleName)> assemblies)
        {
            StartupLogger.LogInformation(InternalLocalizers.General["EXTRA_ASSEMBLY_LOAD", module.Name]);

            var extMod = module.ExtraAssemblies.ToArray();

            foreach (var (asm, filePath) in extMod)
            {
                LoadedAssemblies.Add((module.Name, asm.Name, asm.FullName));
                var hostAsm = HostAssemblies.FirstOrDefault(a => a!.GetName().Name == asm.Name);
                if (hostAsm != null)    // Если нашли сборку которая есть в хосте
                {
                    EnsureAssemblyVersion(asm, filePath);

                    // Сравнение сборок модуля со сборками хоста
                    var ver = asm.AssemblyVersion;
                    var hostVer = hostAsm.GetName().Version;
                    if (ver != null && hostVer != null && ver > hostVer)  // Если версия в хосте ниже версии из модуля
                    {
                        StartupLogger.LogError(InternalLocalizers.General["ASSEMBLY_VERSION_INCOMPATIBLE", asm.Name!, module.Name, ver.ToString(), hostVer.ToString()]);
                        throw new ApplicationException(string.Join("; ", asm.Name!, module.Name, ver.ToString(), hostVer.ToString()));
                    }
                    else
                    {   // Отмечаем что произошла замена сборки на версию из хоста
                        // Загрузка сборки модуля пропускается
                        ReplacesAssemblies.Add(new AssemblyReplacementInfo
                        {
                            Module = HOST_NAME,
                            ReplacedAssembly = asm.Name,
                            ReplacementAssembly = hostAsm.FullName
                        });
                    }
                }
                else
                {
                    var (prevAsm, index) = assemblies.FindWithIndex(a => a.Assembly.Name == asm.Name);

                    if (index < 0)
                        assemblies.Add((asm, filePath, module.Name));
                    else
                    {
                        EnsureAssemblyVersion(asm, filePath);
                        EnsureAssemblyVersion(prevAsm.Assembly, prevAsm.Path);

                        if (asm.AssemblyVersion > prevAsm.Assembly.AssemblyVersion)
                        {
                            StartupLogger.LogInformation(InternalLocalizers.General["ASSEMBLY_REPLACE", prevAsm.Assembly.FullName!, asm.FullName]);
                            assemblies[index] = (asm, filePath, module.Name);
                            ReplacesAssemblies.Add(new AssemblyReplacementInfo
                            {
                                Module = module.Name,
                                ReplacedAssembly = prevAsm.Assembly.Name,
                                ReplacementAssembly = asm.FullName
                            });
                        }
                        else if (asm.AssemblyVersion == prevAsm.Assembly.AssemblyVersion)
                            ReplacesAssemblies.Add(new AssemblyReplacementInfo
                            {
                                Module = prevAsm.ModuleName,
                                ReplacedAssembly = prevAsm.Assembly.Name,
                                ReplacementAssembly = asm.FullName
                            });
                        else
                            ReplacesAssemblies.Add(new AssemblyReplacementInfo
                            {
                                Module = prevAsm.ModuleName,
                                ReplacedAssembly = asm.Name,
                                ReplacementAssembly = prevAsm.Assembly.FullName
                            });
                    }
                }
            }
        }

        /// <summary>
        /// Регистрация модулей
        /// </summary>
        /// <param name="options">Параметры хоста</param>
        /// <param name="configBuilder">Системный билдер настроек</param>
        /// <exception cref="CycleReferenceException">Если в зависимостях модулей присутствует цикл</exception>
        /// <exception cref="MetadataValidationException">Если отсутствуют название или версия модуля, или название содежржит запрещённые символы</exception>
        /// <exception cref="ModuleSelfReferenceException">Если в метаданных присутствует ссылка на самого себя</exception>
        /// <exception cref="MissingDependencyException">Если в метаданных отсутсвует необходимый модуль</exception>
        public static void Register(HostingOptions options, IConfigurationBuilder configBuilder)
        {
            StartupLogger.LogInformation(InternalLocalizers.General["REGISTER_START"]);

            _graph = new ModulesGraph(Metadata(options));
            Options = options;

            if (_graph.MissingDependencies.Count > 0)
            {
                var kv = _graph.MissingDependencies.First();
                throw new ApplicationException(string.Join("; ", kv.Key, kv.Value.First(), _graph.MissingDependencies));
            }

            // Список сборок которые надо загузить в контекст после проверки версий
            var assemblies = new List<(AssemblyInfo Assembly, string Path, string ModuleName)>();

            _graph.TraverseAndExecute(node =>
            {
                var path = Path.Combine(node.Module.Metadata.ModulePath!, $"{node.Module.Name}.dll");
                if (File.Exists(path))
                {
                    var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
                    node.Module.RootAssembly = asm;
                }
                LoadExtraAssemblies(node.Module, assemblies);
                AddConfiguration(configBuilder, node.Module, options);
            });

            foreach (var asm in assemblies)
                try
                {
                    AssemblyLoadContext.Default.LoadFromAssemblyPath(asm.Path);
                }
                catch (Exception ex)
                {
                    StartupLogger.LogError(ex, InternalLocalizers.General["ASSEMBLY_LOAD_ERROR"], asm.Assembly, asm.Path, asm.ModuleName);
                    throw;
                }

            StartupLogger.LogInformation(InternalLocalizers.General["REGISTER_END"]);
        }
    }
}