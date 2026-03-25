using BiomePampa.Application.Services;
using BiomePampa.Infrastructure.Authorization;
using BiomePampa.Infrastructure.Data;
using BiomePampa.Infrastructure.Repositories;
using FluentValidation;
using System.Reflection;

namespace BiomePampa.Api.Configuration;

public static class DependencyInjectionConfiguration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IWorkLogService, WorkLogService>();
        services.AddScoped<IPaymentPeriodService, PaymentPeriodService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IResourcePermissionService, ResourcePermissionService>();

        // Register FluentValidation Validators
        services.AddValidatorsFromAssembly(Assembly.Load("BiomePampa.Application"));

        return services;
    }
}
