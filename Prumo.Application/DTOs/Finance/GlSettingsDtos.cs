namespace Prumo.Application.DTOs.Finance
{
    public record TenantGlSettingsDto(
        Guid TenantId,
        Guid? DefaultCashAccountId,
        string? DefaultCashAccountCode,
        Guid? DefaultAccountsPayableAccountId,
        string? DefaultAccountsPayableAccountCode
    );

    public record UpdateTenantGlSettingsDto(
        Guid? DefaultCashAccountId,
        Guid? DefaultAccountsPayableAccountId
    );
}
