namespace HostLibrary.Services.Interfaces
{
    public interface IJwtAuthService
    {
        public string GetToken(string userName);
    }
}
