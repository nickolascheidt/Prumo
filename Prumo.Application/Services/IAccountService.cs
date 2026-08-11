using Prumo.Application.DTOs.Finance;

namespace Prumo.Application.Services
{
    public interface IAccountService
    {
        Task<IReadOnlyList<AccountDto>> ListAccountsAsync(Guid tenantId, bool includeInactive = false, CancellationToken ct = default);
        Task<AccountDto?> GetAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct = default);
        Task<AccountDto> CreateAccountAsync(Guid tenantId, CreateAccountRequestDto request, CancellationToken ct = default);
        Task<AccountDto> UpdateAccountAsync(Guid tenantId, Guid accountId, UpdateAccountRequestDto request, CancellationToken ct = default);
        Task DeactivateAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct = default);
    }
}
