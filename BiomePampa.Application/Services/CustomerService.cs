using BiomePampa.Application.DTOs.Customers;
using BiomePampa.Domain.Entities;
using BiomePampa.Infrastructure.Repositories;

namespace BiomePampa.Application.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IRepository<Customer> _repository;

        public CustomerService(IRepository<Customer> repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var customers = await _repository.GetAllAsync(cancellationToken);
            return customers.Select(MapToDto);
        }

        public async Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var customer = await _repository.GetByIdAsync(id, cancellationToken);
            return customer != null ? MapToDto(customer) : null;
        }

        public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken cancellationToken = default)
        {
            var customer = new Customer
            {
                Name = dto.Name,
                CompanyName = dto.CompanyName,
                TaxId = dto.TaxId,
                Phone = dto.Phone,
                Email = dto.Email,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                ZipCode = dto.ZipCode,
                Notes = dto.Notes
            };

            var created = await _repository.AddAsync(customer, cancellationToken);
            return MapToDto(created);
        }

        public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerDto dto, CancellationToken cancellationToken = default)
        {
            var customer = await _repository.GetByIdAsync(id, cancellationToken);
            if (customer == null)
                throw new KeyNotFoundException($"Customer with ID {id} not found");

            customer.Name = dto.Name;
            customer.CompanyName = dto.CompanyName;
            customer.TaxId = dto.TaxId;
            customer.Phone = dto.Phone;
            customer.Email = dto.Email;
            customer.Address = dto.Address;
            customer.City = dto.City;
            customer.State = dto.State;
            customer.ZipCode = dto.ZipCode;
            customer.Notes = dto.Notes;

            await _repository.UpdateAsync(customer, cancellationToken);
            return MapToDto(customer);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _repository.DeleteAsync(id, cancellationToken);
        }

        private static CustomerDto MapToDto(Customer customer)
        {
            return new CustomerDto(
                customer.Id,
                customer.Name,
                customer.CompanyName,
                customer.TaxId,
                customer.Phone,
                customer.Email,
                customer.Address,
                customer.City,
                customer.State,
                customer.ZipCode,
                customer.Notes,
                customer.IsActive,
                customer.CreatedAt,
                customer.UpdatedAt
            );
        }
    }
}
