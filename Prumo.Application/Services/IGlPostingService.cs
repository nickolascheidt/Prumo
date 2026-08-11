namespace Prumo.Application.Services
{
    public interface IGlPostingService
    {
        Task PostApPaymentAsync(
            Guid tenantId, Guid apEntryId, string description,
            decimal amount, DateTime paidAt, Guid userId,
            CancellationToken ct = default);
    }
}
