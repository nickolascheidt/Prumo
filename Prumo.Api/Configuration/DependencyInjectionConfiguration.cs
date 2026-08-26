using Prumo.Application.Services;
using Prumo.Domain.Common;
using Prumo.Infrastructure.Authorization;
using Prumo.Infrastructure.Data;
using Prumo.Infrastructure.Multitenancy;
using Prumo.Infrastructure.Services;
using FluentValidation;
using System.Reflection;

namespace Prumo.Api.Configuration;

public static class DependencyInjectionConfiguration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IResourcePermissionService, ResourcePermissionService>();

        // Multi-tenancy
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantRoleService, TenantRoleService>();
        services.AddScoped<ITenantRoleAdminService, TenantRoleAdminService>();

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
        services.AddValidatorsFromAssembly(Assembly.Load("Prumo.Application"));

        return services;
    }
}