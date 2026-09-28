using Prumo.Domain.Entities;
using Prumo.Infrastructure.Authorization;
using Prumo.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Prumo.Api.Configuration;

public static class DatabaseConfiguration
{
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

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

        // Identity's default RoleValidator rejects any name that already exists anywhere,
        // which makes the (NormalizedName, TenantId) index a dead letter: the write never
        // reaches the database for the index to have anything to decide.
        //
        // It has to be REMOVED, not complemented. `AddRoleValidator` appends to the list
        // and RoleManager runs them all — the default would keep refusing.
        services.RemoveAll<IRoleValidator<ApplicationRole>>();
        services.AddScoped<IRoleValidator<ApplicationRole>, TenantScopedRoleValidator>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await DbInitializer.InitializeAsync(app.Services);
    }
}
