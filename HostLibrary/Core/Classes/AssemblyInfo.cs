using System;

namespace HostLibrary.Core.Classes
{
    public class AssemblyInfo
    {
        public AssemblyInfo(string name)
        {
            Name = name;
        }

        public string Name { get; set; }

        public Version Version { get; set; }

        public Version AssemblyVersion { get; set; }

        public string FullName => AssemblyVersion != null || Version != null ? $"\"{Name}\", Version={AssemblyVersion ?? Version}" : $"\"{Name}\"";

        public override string ToString() => FullName;
    }
}