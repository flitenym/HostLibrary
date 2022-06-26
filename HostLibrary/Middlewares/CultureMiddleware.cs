using HostLibrary.Enum;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace HostLibrary.Middlewares
{
    public class CultureMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CultureMiddleware> _logger;

        public CultureMiddleware(RequestDelegate next, ILogger<CultureMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var cultureName = context.Request.Cookies["i18nextLng"];
            if (string.IsNullOrEmpty(cultureName))
                cultureName = context.Request.Headers["Accept-Language"].ToString().Split(',').FirstOrDefault();

            TrySetCulture(cultureName);

            await _next(context);
        }

        /// <summary>
        /// Пытается установить указанную культуру. Не бросает исключений.
        /// </summary>
        public void TrySetCulture(string cultureName)
        {
            var lang = CultureTypeConverter.ConvertStringToCultureType(cultureName);

            if (string.IsNullOrEmpty(lang))
            {
                return;
            }

            var culture = new CultureInfo(lang);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
    }
}