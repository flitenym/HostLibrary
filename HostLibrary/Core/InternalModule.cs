using HostLibrary.Interfaces;
using HostLibrary.StaticClasses;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using HostLibrary.Extensions;
using Microsoft.Extensions.DependencyModel;
using HostLibrary.Core.Classes;

namespace HostLibrary.Core
{
    public class InternalModule
    {
        private AssemblyLoadContext _alc;

        private IModule _innerModule;

        /// <summary>
        /// Сборка в которой распологается экземпляр модуля
        /// </summary>
        public Assembly RootAssembly { get; set; }

        /// <summary>
        /// Информация о модуле
        /// </summary>
        public ModuleMetadata Metadata { get; }

        /// <summary>
        /// Название модуля
        /// </summay>
        public string Name => Metadata.Name;

        /// <summary>
        /// Дополнительные сборки используемые модулем
        /// </summary>
        public IEnumerable<(AssemblyInfo Assembly, string Path)> ExtraAssemblies
        {
            get
            {
                using var reader = new DependencyContextJsonReader();
                string depsPath = Path.Combine(Metadata.ModulePath!, $"{Metadata.Name}.deps.json");
                if (!File.Exists(depsPath))
                    yield break;

                using var file = File.OpenRead(depsPath);

                var dependencies = reader.Read(file);
                foreach (var assemblyName in dependencies.GetDefaultProjectAssemblyNames())
                {
                    var filePath = Path.Combine(Metadata.ModulePath!, $"{assemblyName.Name}.dll");
                    if (assemblyName.Name != Metadata.Name && File.Exists(filePath))
                    {
                        filePath = Path.GetFullPath(filePath);
                        yield return (assemblyName, filePath);
                    }
                }
            }
        }

        public InternalModule(ModuleMetadata metadata, AssemblyLoadContext alc)
        {
            Metadata = metadata;
            _alc = alc;
        }

        public InternalModule(ModuleMetadata metadata) : this(metadata, AssemblyLoadContext.Default) { }

        private void AddParts(ApplicationPartManager manager, IEnumerable<ApplicationPart> parts)
        {
            foreach (var part in parts)
            {
                if (!manager.ApplicationParts.Any(p => p.GetType() == part.GetType() &&
                    string.Equals(p.Name, part.Name, StringComparison.OrdinalIgnoreCase)))
                    manager.ApplicationParts.Add(part);
            }
        }

        public virtual IModule CreateInstance(IConfiguration configuration)
        {
            if (_innerModule == null)
            {
                var type = RootAssembly?.GetExportedTypes().FirstOrDefault(t => typeof(IModule).IsAssignableFrom(t));

                if (type is null) return null;

                var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

                foreach (var ctor in ctors)
                {
                    var parameters = ctor.GetParameters();
                    if (parameters.Length == 0)
                    {
                        _innerModule = Activator.CreateInstance(type) as IModule;
                    }
                    else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IConfiguration))
                    {
                        _innerModule = Activator.CreateInstance(type, configuration) as IModule;
                    }
                }
            }

            return _innerModule;
        }

        public virtual T CreateInstance<T>(params object[] args) where T : class
        {
            var type = RootAssembly?.GetExportedTypes().FirstOrDefault(t => typeof(T).IsAssignableFrom(t));

            if (type is null) return null;

            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            if (args?.Length > 0)
            {
                foreach (var ctor in ctors)
                {
                    var parameters = ctor.GetParameters();
                    if (parameters.SequenceEqual(args))
                        return Activator.CreateInstance(type, args) as T;
                }

                StartupLogger.LogWarning(InternalLocalizers.General["CTOR_ARGS_MISSMATCH"]);
            }

            var paramLessCtor = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);

            if (paramLessCtor == null)
                StartupLogger.LogWarning(InternalLocalizers.General["NO_DEFAULT_CTOR"]);
            else
                return Activator.CreateInstance(type) as T;

            return null;
        }

        public virtual Type FindType<T>() => RootAssembly?.GetExportedTypes().FirstOrDefault(t => typeof(T).IsAssignableFrom(t));

        public virtual void AddParts(ApplicationPartManager partManager)
        {
            var assembly = RootAssembly;
            if (assembly is null) return;
            var partFactory = ApplicationPartFactory.GetApplicationPartFactory(assembly);

            foreach (var part in partFactory.GetApplicationParts(assembly))
                partManager.ApplicationParts.Add(part);

            var relatedParts = RelatedAssemblyAttribute.GetRelatedAssemblies(assembly, true).ToDictionary(
                ra => ra,
                CompiledRazorAssemblyApplicationPartFactory.GetDefaultApplicationParts);

            partManager.ApplicationParts.Add(new AssemblyPart(assembly));

            foreach (var rp in relatedParts)
                AddParts(partManager, rp.Value);
        }

        public string GetFilePath(string fileName) => Path.Combine(Metadata.ModulePath!, fileName);

        public virtual void LoadExtraAssemblies()
        {
            foreach (var asm in ExtraAssemblies)
                _alc.LoadFromAssemblyPath(asm.Path);
        }

        public override int GetHashCode() => Metadata.GetHashCode();
        public override bool Equals(object obj)
        {
            var other = obj as InternalModule;
            if (other == null) return false;

            return Metadata.Equals(other.Metadata);
        }

        public override string ToString() => Metadata.ToString();
    }
}