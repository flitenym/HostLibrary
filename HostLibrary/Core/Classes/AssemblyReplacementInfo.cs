namespace HostLibrary.Core.Classes
{
    public class AssemblyReplacementInfo
    {
        /// <summary>
        /// Полное название сборки которую заменили
        /// </summary>
        public string ReplacedAssembly { get; set; }

        /// <summary>
        /// Полное название сборки которой заменили
        /// </summary>
        public string ReplacementAssembly { get; set; }

        /// <summary>
        /// Название модуля из которого взята сборка на которую заменили
        /// </summary>
        public string Module { get; set; }
    }
}