namespace HostLibrary.Enum
{
    public enum DbProviderType
    {
        SqlServer,
        PostgreSql,
        Undefined
    }

    public static class DbProviderTypeConverter
    {
        public static DbProviderType ConvertStringToDbProvider(string dbProviderTypeString) =>
            dbProviderTypeString switch
            {
                nameof(DbProviderType.SqlServer) => DbProviderType.SqlServer,
                nameof(DbProviderType.PostgreSql) => DbProviderType.PostgreSql,
                _ => DbProviderType.Undefined,
            };
    }
}