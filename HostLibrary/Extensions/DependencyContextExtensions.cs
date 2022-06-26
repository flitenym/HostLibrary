using HostLibrary.Core.Classes;
using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HostLibrary.Extensions
{
    public static class DependencyContextExtensions
    {
        private const string NativeImageSufix = ".ni";

        public static IEnumerable<AssemblyInfo> GetDefaultProjectAssemblyNames(this DependencyContext self)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            return self.RuntimeLibraries.SelectMany(library => library.GetDefaultProjectAssemblyNames(self));
        }

        private static AssemblyInfo GetAssemblyName(this RuntimeLibrary self, string assetPath)
        {
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (name == null)
            {
                throw new ArgumentException($"Provided path has empty file name '{assetPath}'", nameof(assetPath));
            }

            if (name.EndsWith(NativeImageSufix))
            {
                name = name.Substring(0, name.Length - NativeImageSufix.Length);
            }

            var assemblyName = new AssemblyInfo(name);
            if (assemblyName.Version == null)
            {
                var rf = self.RuntimeAssemblyGroups.FirstOrDefault(g => g.Runtime == string.Empty)?.RuntimeFiles.FirstOrDefault();
                Version version = null;
                if (Version.TryParse(rf?.AssemblyVersion, out version))
                    assemblyName.AssemblyVersion = version;

                var i = self.Version.IndexOf('-');
                string s;
                if (i > -1)
                    s = self.Version[..i];
                else
                    s = self.Version;
                if (Version.TryParse(s, out version))
                {
                    assemblyName.Version = version;
                }
            }
            return assemblyName;
        }

        public static IEnumerable<AssemblyInfo> GetDefaultProjectAssemblyNames(this RuntimeLibrary self, DependencyContext context)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            return ResolveAssets(context, string.Empty, self.RuntimeAssemblyGroups).Select(self.GetAssemblyName);
        }

        private static IEnumerable<string> ResolveAssets(
            DependencyContext context,
            string runtimeIdentifier,
            IEnumerable<RuntimeAssetGroup> assets)
        {
            RuntimeFallbacks fallbacks = context.RuntimeGraph.FirstOrDefault(f => f.Runtime == runtimeIdentifier);
            IEnumerable<string> rids = Enumerable.Concat(new[] { runtimeIdentifier }, fallbacks?.Fallbacks ?? Enumerable.Empty<string>());
            return SelectAssets(rids, assets);
        }

        private static IEnumerable<string> SelectAssets(IEnumerable<string> rids, IEnumerable<RuntimeAssetGroup> groups)
        {
            var array = groups.ToArray();
            foreach (string rid in rids)
            {
                RuntimeAssetGroup group = array.FirstOrDefault(g => g.Runtime == rid);
                if (group != null)
                {
                    return group.AssetPaths;
                }
            }

            // Return the RID-agnostic group
            return array.GetDefaultAssets();
        }
    }
}
