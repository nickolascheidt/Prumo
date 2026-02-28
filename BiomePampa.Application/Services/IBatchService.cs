using BiomePampa.Application.DTOs.Batches;

namespace BiomePampa.Application.Services
{
    public interface IBatchService
    {
        Task<IEnumerable<BatchDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<BatchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<BatchDto> CreateAsync(CreateBatchDto dto, CancellationToken cancellationToken = default);
        Task<BatchDto> UpdateAsync(Guid id, UpdateBatchDto dto, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
