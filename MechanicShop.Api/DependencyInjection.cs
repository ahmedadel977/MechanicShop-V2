using System;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using MechanicShop.Api.Infrastructure;
using MechanicShop.Api.OpenApi.Transformers;
using MechanicShop.Api.Services;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        services.AddCustomProblemDetails()
                .AddCustomApiVersioning()
                .AddApiDocumentation()
                .AddExceptionHandling()
                .AddControllerWithJsonConfiguration()
                .AddValidation()
                .AddConfiguredCors(configuration)
                .AddIdentityInfrastructure()
                .AddAppRateLimiting(configuration)
                .AddAppOutputCaching()
                .AddAppOpenTelememrty()
                .AddSignalR();

        return services;
    }

    public static IServiceCollection AddAppOutputCaching(this IServiceCollection services)
    {
        services.AddOutputCache(options =>
        {
            options.SizeLimit = 100 * 1024 * 1024; // 100 mb
            options.AddBasePolicy(policy =>
                policy.Expire(TimeSpan.FromSeconds(60))
                    .SetVaryByHeader("Authorization"));
        });

        return services;
    }

    public static IServiceCollection AddAppOpenTelememrty(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
        .ConfigureResource(res => res.AddService("overservice"))
        .WithTracing(static tracing =>
        {
            tracing.AddAspNetCoreInstrumentation().
            AddHttpClientInstrumentation();

            tracing.AddOtlpExporter();
        }).
        WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation().
            AddHttpClientInstrumentation();

            metrics.AddOtlpExporter();
            
        });

        return services;
    }

    
public static IServiceCollection AddAppRateLimiting(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var section = configuration.GetSection("RateLimiting");
    var settings = section.Get<RateLimitSettings>() ?? new RateLimitSettings();
    if (!settings.IsValid)
    {
        throw new InvalidOperationException("RateLimiting values must all be positive.");
    }

    services.AddOptions<RateLimitSettings>()
        .Bind(section)
        .Validate(value => value.IsValid, "RateLimiting values must all be positive.")
        .ValidateOnStart();

    var knownProxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
    services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.RequireHeaderSymmetry = true;
        foreach (var proxy in knownProxies)
        {
            if (!IPAddress.TryParse(proxy, out var address))
            {
                throw new InvalidOperationException("ForwardedHeaders:KnownProxies must contain valid IP addresses.");
            }

            options.KnownProxies.Add(address);
        }
    });

    services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = settings.GlobalPermitLimit,
                        Window = TimeSpan.FromMinutes(settings.GlobalWindowMinutes),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    })),
            PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetConcurrencyLimiter(
                    "GlobalConcurrency",
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = settings.GlobalConcurrencyLimit,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    })));

        options.AddPolicy("LoginPolicy", context => FixedWindow(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            settings.LoginPermitLimit,
            settings.LoginWindowMinutes));

        options.AddPolicy("RefreshPolicy", context => FixedWindow(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            settings.RefreshPermitLimit,
            settings.RefreshWindowMinutes));

        options.AddPolicy("ApiUserPolicy", context => FixedWindow(
            UserPartition(context),
            settings.AuthenticatedUserPermitLimit,
            settings.AuthenticatedUserWindowMinutes));

        options.AddPolicy("RegisterPolicy", context => FixedWindow(
            UserPartition(context),
            settings.AccountCreationPermitLimit,
            settings.AccountCreationWindowMinutes));

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, _) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            }

            return ValueTask.CompletedTask;
        };
    });

    return services;
}

private static RateLimitPartition<string> FixedWindow(
    string partitionKey,
    int permitLimit,
    int windowMinutes) =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(windowMinutes),
            QueueLimit = 0,
            AutoReplenishment = true
        });

private static string UserPartition(HttpContext context) =>
    context.User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? context.Connection.RemoteIpAddress?.ToString()
    ?? "unknown";


    public static IServiceCollection AddCustomProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = (context) =>
        {
            context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
            context.ProblemDetails.Extensions.Add("requestId", context.HttpContext.TraceIdentifier);
        });

        return services;
    }

    public static IServiceCollection AddCustomApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        }).AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        string[] versions = ["v1"];

        foreach (var version in versions)
        {
            services.AddOpenApi(version, options =>
            {
                // Versioning config
                options.AddDocumentTransformer<VersionInfoTransformer>();

                // Security Scheme config
                options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();

                // Security Operation config
                options.AddOperationTransformer<BearerSecurityOperationTransformer>();
            });
        }

        return services;
    }

    public static IServiceCollection AddExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    public static IServiceCollection AddControllerWithJsonConfiguration(this IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(options => options
            .JsonSerializerOptions
            .DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

        return services;
    }

    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        return services;
    }

    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IUser, CurrentUser>();
        services.AddHttpContextAccessor();
        return services;
    }

    public static IServiceCollection AddConfiguredCors(this IServiceCollection services, IConfiguration configuration)
    {
        var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>()!;

        services.AddCors(options => options.AddPolicy(
            appSettings.CorsPolicyName,
            policy => policy
                .WithOrigins(appSettings.AllowedOrigins!)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

        return services;
    }

    public static IApplicationBuilder UseCoreMiddlewares(this IApplicationBuilder app, IConfiguration configuration)
    {
        // 1. Exception handling should be FIRST to catch all errors
        app.UseExceptionHandler();

        // 2. Status code pages for handling HTTP status codes
        app.UseStatusCodePages();

        app.UseForwardedHeaders();

        app.UseHttpsRedirection();

        // 4. Serilog request logging (early to log all requests)
        app.UseSerilogRequestLogging();

        // 5. CORS (before authentication/authorization)
        app.UseCors(configuration["AppSettings:CorsPolicyName"]!);

        app.UseAuthentication();

        app.UseRateLimiter();

        app.UseAuthorization();

        // 9. Output caching (after auth to cache based on user context)
        app.UseOutputCache();
     

        return app;
    }
}
