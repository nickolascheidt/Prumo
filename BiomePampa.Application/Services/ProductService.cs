using BiomePampa.Application.DTOs.Products;
using BiomePampa.Domain.Entities;
using BiomePampa.Infrastructure.Repositories;
using FluentValidation;

namespace BiomePampa.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IRepository<Product> _repository;
        private readonly IValidator<CreateProductDto> _createValidator;
        private readonly IValidator<UpdateProductDto> _updateValidator;

        public ProductService(
            IRepository<Product> repository,
            IValidator<CreateProductDto> createValidator,
            IValidator<UpdateProductDto> updateValidator)
        {
            _repository = repository;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
        }

        public async Task<IEnumerable<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var products = await _repository.GetAllAsync(cancellationToken);
            return products.Select(MapToDto);
        }

        public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var product = await _repository.GetByIdAsync(id, cancellationToken);
            return product != null ? MapToDto(product) : null;
        }

        public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
        {
            // Validar DTO
            await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                SKU = dto.SKU,
                OliveOilType = dto.OliveOilType,
                Volume = dto.Volume,
                Barcode = dto.Barcode,
                MinimumStock = dto.MinimumStock,
                MaximumStock = dto.MaximumStock,
                UnitPrice = dto.UnitPrice
            };

            var created = await _repository.AddAsync(product, cancellationToken);
            return MapToDto(created);
        }

        public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto dto, CancellationToken cancellationToken = default)
        {
            // Validar DTO
            await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

            var product = await _repository.GetByIdAsync(id, cancellationToken);
            if (product == null)
                throw new KeyNotFoundException($"Product with ID {id} not found");

            product.Name = dto.Name;
            product.Description = dto.Description;
            product.SKU = dto.SKU;
            product.OliveOilType = dto.OliveOilType;
            product.Volume = dto.Volume;
            product.Barcode = dto.Barcode;
            product.MinimumStock = dto.MinimumStock;
            product.MaximumStock = dto.MaximumStock;
            product.UnitPrice = dto.UnitPrice;

            await _repository.UpdateAsync(product, cancellationToken);
            return MapToDto(product);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _repository.DeleteAsync(id, cancellationToken);
        }

        private static ProductDto MapToDto(Product product)
        {
            return new ProductDto(
                product.Id,
                product.Name,
                product.Description,
                product.SKU,
                product.OliveOilType,
                product.Volume,
                product.Barcode,
                product.MinimumStock,
                product.MaximumStock,
                product.UnitPrice,
                product.IsActive,
                product.CreatedAt,
                product.UpdatedAt
            );
        }
    }
}
