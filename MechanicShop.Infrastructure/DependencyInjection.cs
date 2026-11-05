using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.BackGroundJobs;
using MechanicShop.Infrastructure.Date;
using MechanicShop.Infrastructure.Date.Interceptors;
using MechanicShop.Infrastructure.identity;
using MechanicShop.Infrastructure.identity.policies;
using MechanicShop.Infrastructure.Realtime;
using MechanicShop.Infrastructure.Services;
using MechanicShop.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailSettings>(
        configuration.GetSection("EmailSettings"));






        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentBranchContext, CurrentBranchContext>();
        services.AddScoped<ICurrentEmployeeAccess, CurrentEmployeeAccessService>();
        services.AddScoped<IEmployeeAccountService, EmployeeAccountService>();

        var connectionString = configuration.GetConnectionString("DefaultConnection");


        ArgumentNullException.ThrowIfNull(connectionString);
        

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();


        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(connectionString);
        });
        services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "sqlserver",
        tags: ["database", "ready"]);

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());


            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                var jwtSettings = configuration.GetSection("JwtSettings");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                           Encoding.UTF8.GetBytes(jwtSettings["Secret"]!)),
                };
            });

            services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequiredUniqueChars = 1;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        // SCOPE AUZ

        services.AddScoped<IAuthorizationHandler, LaborAssignedHandler>();
        services.AddScoped<IAuthorizationHandler, ActiveBranchAccessHandler>();


        services.AddAuthorizationBuilder()
              .AddPolicy("ManagerOnly", policy => policy.RequireRole("Manager"))
              .AddPolicy("SelfScopedWorkOrderAccess", policy =>
                policy.Requirements.Add(new LaborAssignedRequirement()))
               .AddPolicy("CompanyOnly", policy =>
               {
                   policy.RequireRole("Manager");
                   policy.RequireClaim("access_scope", "Company");
               })
              .AddPolicy("ActiveBranchAccess", policy =>
                policy.Requirements.Add(new ActiveBranchAccessRequirement()));

        services.AddTransient<IIdentityService, IdentityService>();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
            // برنامج Redis يحتاج إلى اسم فريد لتحديد مثيل التخزين المؤقت، يمكنك تغييره حسب الحاجة
            options.InstanceName = "M02";
        });

        services.AddHealthChecks()
         .AddRedis(
        redisConnectionString:
            configuration.GetConnectionString("Redis")!,
        name: "redis",
        tags: ["cache", "ready"]);


        services.AddHybridCache(options => options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10), // L2, L3
                LocalCacheExpiration = TimeSpan.FromSeconds(30), // L1
            });
           // ادخال داتا
            services.AddScoped<ApplicationDbContextInitialiser>();
            services.AddScoped<CompanyManagerBootstrapper>();
            services.AddScoped<IWorkOrderPolicy, WorkOrderPolicy>();

            services.AddScoped<ITokenProvider, TokenProvider>();

            services.AddScoped<IInvoicePdfGenerator, InvoicePdfGenerator>();

            services.AddScoped<INotificationService, NotificationService>();

            services.AddScoped<IWorkOrderNotifier, SignalRWorkOrderNotifier>();
            // backGOUND
            services.AddHostedService<OverdueBookingCleanupService>();

        return services;
        }

    }
