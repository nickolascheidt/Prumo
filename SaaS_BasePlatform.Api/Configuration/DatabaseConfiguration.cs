using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SaaS_BasePlatform.Api.Configuration;

public static class DatabaseConfiguration
{
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            // Password settings
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;

            // User settings
            options.User.RequireUniqueEmail = true;

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await DbInitializer.InitializeAsync(app.Services);
    }
}
