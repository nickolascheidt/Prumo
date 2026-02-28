using BiomePampa.Application.DTOs.Batches;
using BiomePampa.Domain.Entities;
using BiomePampa.Infrastructure.Repositories;

namespace BiomePampa.Application.Services
{
    public class BatchService : IBatchService
    {
        private readonly IRepository<Batch> _repository;

        public BatchService(IRepository<Batch> repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<BatchDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var batches = await _repository.GetAllAsync(cancellationToken);
            return batches.Select(MapToDto);
        }

        public async Task<BatchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var batch = await _repository.GetByIdAsync(id, cancellationToken);
            return batch != null ? MapToDto(batch) : null;
        }

        public async Task<BatchDto> CreateAsync(CreateBatchDto dto, CancellationToken cancellationToken = default)
        {
            var batch = new Batch
            {
                BatchNumber = dto.BatchNumber,
                ProductId = dto.ProductId,
                ProductionDate = dto.ProductionDate,
                ExpirationDate = dto.ExpirationDate,
                InitialQuantity = dto.InitialQuantity,
                CurrentQuantity = dto.InitialQuantity,
                SupplierId = dto.SupplierId,
                Notes = dto.Notes
            };

            var created = await _repository.AddAsync(batch, cancellationToken);
            return MapToDto(created);
        }

        public async Task<BatchDto> UpdateAsync(Guid id, UpdateBatchDto dto, CancellationToken cancellationToken = default)
        {
            var batch = await _repository.GetByIdAsync(id, cancellationToken);
            if (batch == null)
                throw new KeyNotFoundException($"Batch with ID {id} not found");

            batch.BatchNumber = dto.BatchNumber;
            batch.ProductionDate = dto.ProductionDate;
            batch.ExpirationDate = dto.ExpirationDate;
            batch.CurrentQuantity = dto.CurrentQuantity;
            batch.SupplierId = dto.SupplierId;
            batch.Notes = dto.Notes;

            await _repository.UpdateAsync(batch, cancellationToken);
            return MapToDto(batch);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _repository.DeleteAsync(id, cancellationToken);
        }

        private static BatchDto MapToDto(Batch batch)
        {
            return new BatchDto(
                batch.Id,
                batch.BatchNumber,
                batch.ProductId,
                batch.Product?.Name ?? string.Empty,
                batch.ProductionDate,
                batch.ExpirationDate,
                batch.InitialQuantity,
                batch.CurrentQuantity,
                batch.SupplierId,
                batch.Supplier?.Name ?? null,
                batch.Notes,
                batch.IsActive,
                batch.CreatedAt,
                batch.UpdatedAt
            );
        }
    }
}
