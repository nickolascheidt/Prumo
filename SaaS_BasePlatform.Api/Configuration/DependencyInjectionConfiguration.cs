using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Infrastructure.Authorization;
using SaaS_BasePlatform.Infrastructure.Data;
using SaaS_BasePlatform.Infrastructure.Multitenancy;
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

        // Multi-tenancy
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantService, TenantService>();

        // Accounts Payable
        services.AddScoped<IAccountsPayableService, AccountsPayableService>();

        // General Ledger
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IJournalService, JournalService>();
        services.AddScoped<ITenantGlSettingsService, TenantGlSettingsService>();
        services.AddScoped<IGlPostingService, GlPostingService>();

        // HR Module
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IWorkLogService, WorkLogService>();
        services.AddScoped<IPaymentPeriodService, PaymentPeriodService>();
        services.AddScoped<IPaymentService, PaymentService>();

        // Register FluentValidation Validators
        services.AddValidatorsFromAssembly(Assembly.Load("SaaS_BasePlatform.Application"));

        return services;
    }
}