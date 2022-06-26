namespace HostLibrary.Enum
{
    public enum CultureType
    {
        ru,
        en
    }

    public static class CultureTypeConverter
    {
        public static string ConvertStringToCultureType(string cultureName) =>
            cultureName switch
            {
                nameof(CultureType.ru) => nameof(CultureType.ru),
                nameof(CultureType.en) => nameof(CultureType.en),
                _ => null,
            };
    }
}