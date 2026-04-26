using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Infrastructure.Authorization;
using SaaS_BasePlatform.Infrastructure.Data;
using SaaS_BasePlatform.Infrastructure.Repositories;
using SaaS_BasePlatform.Infrastructure.Services;
using FluentValidation;
using System.Reflection;

namespace SaaS_BasePlatform.Api.Configuration;

public static class DependencyInjectionConfiguration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IResourcePermissionService, ResourcePermissionService>();

        // Register FluentValidation Validators
        services.AddValidatorsFromAssembly(Assembly.Load("SaaS_BasePlatform.Application"));

        return services;
    }
}