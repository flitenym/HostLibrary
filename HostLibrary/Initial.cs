using HostLibrary.Enum;
using HostLibrary.Extensions;
using HostLibrary.Interfaces;
using HostLibrary.Middlewares;
using HostLibrary.Services;
using HostLibrary.Services.Interfaces;
using HostLibrary.StaticClasses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NLog.Web;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HostLibrary
{
    public static class Initial
    {
        #region Подключение модулей

        private static IModule[] GetModules(IConfiguration configuration) => StartupManager.Graph?.Select(m => m.CreateInstance(configuration)).Where(m => m != null).Cast<IModule>().ToArray() ??
                Array.Empty<IModule>();

        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.RequireHttpsMetadata = false;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            // укзывает, будет ли валидироваться издатель при валидации токена
                            ValidateIssuer = true,
                            // строка, представляющая издателя
                            ValidIssuer = configuration.GetSection("Project:Jwt:Issuer").Get<string>(),

                            // будет ли валидироваться потребитель токена
                            ValidateAudience = true,
                            // установка потребителя токена
                            ValidAudience = configuration.GetSection("Project:Jwt:Audience").Get<string>(),
                            // будет ли валидироваться время существования
                            ValidateLifetime = configuration.GetSection("Project:Jwt:ValidateLifetime").Get<bool>(),

                            // установка ключа безопасности
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(configuration.GetSection("Project:Jwt:Key").Get<string>())),
                            // валидация ключа безопасности
                            ValidateIssuerSigningKey = true,
                        };
                    });

            services.Configure<RequestLocalizationOptions>(options =>
            {
                options.DefaultRequestCulture = new RequestCulture(nameof(CultureType.ru));
                options.SupportedCultures = new List<CultureInfo>() {
                    new CultureInfo(nameof(CultureType.ru)),
                    new CultureInfo(nameof(CultureType.en))
                };
                options.SupportedUICultures = new List<CultureInfo>() {
                    new CultureInfo(nameof(CultureType.ru)),
                    new CultureInfo(nameof(CultureType.en))
                };
            });

            services.AddControllers();

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder =>
                    {
                        builder
                          .AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                    });
            });

            services.AddScoped<IJwtAuthService, JwtAuthService>();

            var _modules = GetModules(configuration);

            foreach (var m in _modules)
            {
                m.ConfigureServicesAsync(services).Wait();
            }

            services.Configure<ExceptionHandlerOptions>(options =>
            {
                options.ExceptionHandlingPath = "/Error";
            });
        }

        public static void Configure(IApplicationBuilder app, IHostApplicationLifetime hal, IWebHostEnvironment env, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            app.UseExceptionHandler("/error");

            app.UseForwardedHeaders();

            app.UseRequestLocalization();

            app.UseMiddleware<CultureMiddleware>();

            app.UseStaticFiles();

            var _modules = GetModules(configuration);

            foreach (var m in _modules)
            {
                m.ConfigureAsync(app, hal, env, serviceProvider).Wait();
            }

            app.UseRouting();

            app.UseCors("CorsPolicy");

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        #endregion

        public static IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
                .ConfigureModules()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Trace);
                })
                .UseNLog();
    }
}