using HostLibrary.StaticClasses;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace HostLibrary.Core
{
    /// <summary>
    /// Информация о модуле
    /// </summary>
    public class ModuleMetadata
    {
        private static readonly Regex invalidFileNameChars = new Regex("[" + Regex.Escape(new string(Path.GetInvalidFileNameChars())) + "]", RegexOptions.Compiled);

        private string _key;

        /// <summary>
        /// Название
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Путь к директории с файлами
        /// </summary>
        [JsonPropertyName("Path")]
        public string ModulePath { get; set; } = string.Empty;

        /// <summary>
        /// Версия
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Список зависимостей
        /// </summary>
        public Dictionary<string, string> Dependencies { get; set; }

        /// <summary>
        /// Дополнительные файлы с настройками
        /// </summary>
        [JsonPropertyName("Extra Settings")]
        public string[] ExtraSettingsFiles { get; set; }

        /// <summary>
        /// Путь к файлу настроек модуля
        /// </summary>
        [JsonIgnore]
        public string SettingsPath { get; set; }

        /// <summary>
        /// Порядок загрузки метаданных с диска
        /// </summary>
        [JsonIgnore]
        public int Order { get; set; }

        public string Key
        {
            get
            {
                if (_key == null)
                    _key = $"{Name}:{Version}";

                return _key;
            }
        }

        /// <summary>
        /// Проверка корректности метаданных
        /// </summary>
        /// <exception cref="MetadataValidationException">Если отсутствуют название или версия модуля, или название содежржит запрещённые символы</exception>
        /// <exception cref="ModuleSelfReferenceException">Если в метаданных присутствует ссылка на самого себя</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ApplicationException(InternalLocalizers.General["MD_VAL_NO_NAME_ERR"]!);
            else if (invalidFileNameChars.IsMatch(Name))
                throw new ApplicationException(InternalLocalizers.General["MD_VAL_NAME_ERR"]!);
            if (string.IsNullOrWhiteSpace(Version))
                throw new ApplicationException(InternalLocalizers.General["MD_VAL_NO_VER_ERR"]!);
            if (Dependencies?.ContainsKey(Name) == true)
                throw new ApplicationException(Name);
        }

        /// <summary>
        /// Проверка пути модуля
        /// </summary>
        /// <exception cref="MetadataValidationException">Если путь к папке модуля в метаданных несуществет</exception>
        public void CheckModulePath()
        {
            if (!Directory.Exists(ModulePath))
                throw new ApplicationException(InternalLocalizers.General["MD_VAL_PATH_ERR", ModulePath]!);
        }

        /// <summary>
        /// Сравнение версии модуля
        /// </summary>
        public bool IsVersionFits(string version) => Version == version;

        public override string ToString() => Key;

        public override int GetHashCode() => ModulePath.GetHashCode();

        public override bool Equals(object obj)
        {
            var other = obj as ModuleMetadata;
            if (other == null) return false;
            return ModulePath == other.ModulePath;
        }
    }
}