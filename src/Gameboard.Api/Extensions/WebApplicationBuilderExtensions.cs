using System;
using System.IO;
using Gameboard.Api.Common.Services;
using Gameboard.Api.Data;
using Gameboard.Api.Structure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceStack.Text;

namespace Gameboard.Api.Extensions;

internal static class WebApplicationBuilderExtensions
{
    public static AppSettings BuildAppSettings(this WebApplicationBuilder builder, ILogger logger)
    {
        var settings = builder.Configuration.Get<AppSettings>() ?? new AppSettings();

        settings.Cache.SharedFolder = Path.Combine(
            builder.Environment.ContentRootPath,
            settings.Cache.SharedFolder ?? string.Empty
        );

        settings.Core.ImageFolder = Path.Combine(
            builder.Environment.ContentRootPath,
            settings.Core.ImageFolder ?? string.Empty
        );

        settings.Core.WebHostRoot = builder.Environment.ContentRootPath;

        if (settings.Core.ChallengeDocUrl.IsEmpty())
            settings.Core.ChallengeDocUrl = settings.PathBase;

        if (!settings.Core.ChallengeDocUrl?.EndsWith("/") ?? true)
            settings.Core.ChallengeDocUrl += "/";

        Directory.CreateDirectory(settings.Core.ImageFolder);

        settings.Core.TempDirectory = Path.Combine
        (
            builder.Environment.ContentRootPath,
            settings.Core.TempDirectory = "wwwroot/temp"
        );

        settings.Core.TemplatesDirectory = Path.Combine
        (
            builder.Environment.ContentRootPath,
            settings.Core.TemplatesDirectory ?? "wwwroot/templates"
        );

        CsvConfig<Tuple<string, string>>.OmitHeaders = true;
        CsvConfig<Tuple<string, string, string>>.OmitHeaders = true;
        CsvConfig<ChallengeStatsExport>.OmitHeaders = true;
        CsvConfig<ChallengeDetailsExport>.OmitHeaders = true;

        if (builder.Environment.IsDevOrTest())
            settings.Oidc.RequireHttpsMetadata = false;

        return settings;
    }

    public static void ConfigureServices(this WebApplicationBuilder builder, AppSettings settings)
    {
        var services = builder.Services;

        services
            .AddMvc()
            .AddGameboardJsonOptions();

        services
            .ConfigureForwarding(settings.Headers.Forwarding)
            .AddCors(opt => opt.AddPolicy(settings.Headers.Cors.Name, settings.Headers.Cors.Build()))
            .AddCache(() => settings.Cache);

        if (settings.OpenApi.Enabled)
            services.AddSwagger(settings.Oidc, settings.OpenApi);

        services.AddDataProtection()
            .SetApplicationName(AppConstants.DataProtectionPurpose)
            .PersistKeys(() => settings.Cache);

        services
            .AddSingleton(_ => settings.Core)
            .AddSingleton(_ => settings.Crucible)
            .AddGameboardData(builder.Environment, settings.Database)
            .AddGameboardMediatR()
            .AddGameboardServices(settings)
            .AddConfiguredHttpClients(settings.Core)
            .AddDefaults(settings.Defaults, builder.Environment.ContentRootPath);

        // HOSTED SERVICES
        // don't add these during test - we don't want them interfere with CI
        if (!builder.Environment.IsTest())
            services
                .AddHostedService<BackgroundAsyncTaskRunner>()
                .AddHostedService<JobService>();

        services.AddSingleton
        (
            new AutoMapper.MapperConfiguration(cfg =>
            {
                cfg.AddGameboardMaps();
            }).CreateMapper()
        );
        // configuring SignalR involves acting on the builder as well as its services
        builder.AddGameboardSignalRServices();

        // Configure Auth
        services.AddConfiguredAuthentication(settings.Oidc, settings.ApiKey, builder.Environment);
        services.AddConfiguredAuthorization();

        if (settings.Logging.EnableHttpLogging)
        {
            services.AddHttpLogging(logging =>
            {
                logging.LoggingFields = HttpLoggingFields.ResponseStatusCode
                    | HttpLoggingFields.ResponseBody
                    | HttpLoggingFields.RequestPath
                    | HttpLoggingFields.RequestQuery
                    | HttpLoggingFields.RequestBody;
                logging.RequestBodyLogLimit = settings.Logging.RequestBodyLogLimit;
                logging.ResponseBodyLogLimit = settings.Logging.ResponseBodyLogLimit;
                logging.MediaTypeOptions.AddText(MimeTypes.ApplicationJson);
            });
        }
    }
}
