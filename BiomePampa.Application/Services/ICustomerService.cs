using BiomePampa.Application.DTOs.Customers;

namespace BiomePampa.Application.Services
{
    public interface ICustomerService
    {
        Task<IEnumerable<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken cancellationToken = default);
        Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerDto dto, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
