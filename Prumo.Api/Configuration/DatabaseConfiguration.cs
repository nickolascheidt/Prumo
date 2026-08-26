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

        // O RoleValidator padrão do Identity rejeita qualquer nome já existente em
        // qualquer lugar, o que torna o índice (NormalizedName, TenantId) letra morta:
        // a escrita nunca chega ao banco para o índice ter o que decidir.
        //
        // Precisa ser REMOVIDO, não complementado. `AddRoleValidator` acrescenta à lista
        // e o RoleManager roda todos — o padrão continuaria recusando.
        services.RemoveAll<IRoleValidator<ApplicationRole>>();
        services.AddScoped<IRoleValidator<ApplicationRole>, TenantScopedRoleValidator>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await DbInitializer.InitializeAsync(app.Services);
    }
}
