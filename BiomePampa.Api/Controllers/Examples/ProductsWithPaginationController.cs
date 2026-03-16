// EXEMPLO: Como implementar paginação em um controller
// Este é um exemplo de referência para implementar nos controllers existentes

using BiomePampa.Application.DTOs.Products;
using BiomePampa.Application.Services;
using BiomePampa.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiomePampa.Api.Controllers.Examples
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductsWithPaginationController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsWithPaginationController(IProductService productService)
        {
            _productService = productService;
        }

        /// <summary>
        /// Obter produtos com paginação, busca e ordenação
        /// </summary>
        /// <param name="parameters">Parâmetros de paginação</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista paginada de produtos</returns>
        /// <remarks>
        /// Exemplo de requisição:
        /// 
        ///     GET /api/productsWithPagination?pageNumber=1&amp;pageSize=10&amp;searchTerm=azeite&amp;sortBy=Name&amp;sortDescending=false
        ///     
        /// Parâmetros:
        /// - **pageNumber**: Número da página (padrão: 1)
        /// - **pageSize**: Itens por página (máximo: 100, padrão: 10)
        /// - **searchTerm**: Termo de busca (opcional)
        /// - **sortBy**: Campo para ordenação (opcional)
        /// - **sortDescending**: Ordenar decrescente (padrão: false)
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<ProductDto>>> GetPaged(
            [FromQuery] PaginationParameters parameters,
            CancellationToken cancellationToken)
        {
            // TODO: Implementar método GetPagedAsync no IProductService
            // var result = await _productService.GetPagedAsync(parameters, cancellationToken);
            // return Ok(result);
            
            return Ok(new
            {
                Message = "Este é um exemplo. Implemente GetPagedAsync no ProductService.",
                Parameters = parameters
            });
        }

        /// <summary>
        /// Exemplo de resposta paginada
        /// </summary>
        [HttpGet("example-response")]
        [ProducesResponseType(typeof(PagedResult<object>), StatusCodes.Status200OK)]
        public IActionResult GetExampleResponse()
        {
            var exampleResponse = new PagedResult<object>(
                items: new[]
                {
                    new { Id = Guid.NewGuid(), Name = "Azeite Extra Virgem 500ml", Price = 45.90m },
                    new { Id = Guid.NewGuid(), Name = "Azeite Virgem 1L", Price = 78.50m },
                },
                count: 25, // Total de registros
                pageNumber: 1,
                pageSize: 10
            );

            return Ok(exampleResponse);
        }
    }
}

/*
 * IMPLEMENTAÇÃO NO SERVICE:
 * 
 * public interface IProductService
 * {
 *     Task<PagedResult<ProductDto>> GetPagedAsync(
 *         PaginationParameters parameters, 
 *         CancellationToken cancellationToken = default);
 * }
 * 
 * public class ProductService : IProductService
 * {
 *     private readonly IRepository<Product> _repository;
 *     private readonly ApplicationDbContext _context; // ou use Repository
 *     
 *     public async Task<PagedResult<ProductDto>> GetPagedAsync(
 *         PaginationParameters parameters, 
 *         CancellationToken cancellationToken = default)
 *     {
 *         var query = _context.Products
 *             .Where(p => p.IsActive)
 *             .AsQueryable();
 *         
 *         // Aplicar busca
 *         if (!string.IsNullOrEmpty(parameters.SearchTerm))
 *         {
 *             query = query.Where(p => 
 *                 p.Name.Contains(parameters.SearchTerm) ||
 *                 p.Description.Contains(parameters.SearchTerm) ||
 *                 p.SKU.Contains(parameters.SearchTerm)
 *             );
 *         }
 *         
 *         // Aplicar ordenação
 *         if (!string.IsNullOrEmpty(parameters.SortBy))
 *         {
 *             query = query.ApplySorting(parameters.SortBy, parameters.SortDescending);
 *         }
 *         else
 *         {
 *             query = query.OrderBy(p => p.Name); // Ordenação padrão
 *         }
 *         
 *         // Aplicar paginação
 *         var pagedResult = await query
 *             .Select(p => new ProductDto(
 *                 p.Id,
 *                 p.Name,
 *                 p.Description,
 *                 p.SKU,
 *                 p.OliveOilType,
 *                 p.Volume,
 *                 p.Barcode,
 *                 p.MinimumStock,
 *                 p.MaximumStock,
 *                 p.UnitPrice,
 *                 p.IsActive,
 *                 p.CreatedAt,
 *                 p.UpdatedAt
 *             ))
 *             .ToPagedListAsync(parameters.PageNumber, parameters.PageSize, cancellationToken);
 *         
 *         return pagedResult;
 *     }
 * }
 */
