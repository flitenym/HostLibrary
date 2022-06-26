using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace HostLibrary.StaticClasses
{
    public static class InternalLocalizers
    {
        private const string LOCATION = "HostLibrary";
        public static IStringLocalizer General { get; }

        static InternalLocalizers()
        {
            var options = Microsoft.Extensions.Options.Options.Create(new LocalizationOptions
            {
                ResourcesPath = "Localization"
            });
            General = new ResourceManagerStringLocalizerFactory(options, new LoggerFactory()).Create("HostLibrary.General", LOCATION);
        }
    }
}