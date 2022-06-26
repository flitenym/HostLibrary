namespace HostLibrary.Core.Classes
{
    public class HostingOptions
    {
        /// <summary>
        /// Путь к папке с информацией о модулях
        /// </summary>
        public string Configurations { get; set; }

        /// <summary>
        /// Путь к папке с файлами модулей
        /// </summary>
        public string Installations { get; set; }
    }
}