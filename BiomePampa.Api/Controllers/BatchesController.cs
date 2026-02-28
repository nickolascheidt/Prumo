using BiomePampa.Application.DTOs.Batches;
using BiomePampa.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiomePampa.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BatchesController : ControllerBase
    {
        private readonly IBatchService _batchService;

        public BatchesController(IBatchService batchService)
        {
            _batchService = batchService;
        }

        /// <summary>
        /// Get all batches
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<BatchDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<BatchDto>>> GetAll(CancellationToken cancellationToken)
        {
            var batches = await _batchService.GetAllAsync(cancellationToken);
            return Ok(batches);
        }

        /// <summary>
        /// Get batch by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BatchDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BatchDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var batch = await _batchService.GetByIdAsync(id, cancellationToken);
            
            if (batch == null)
                return NotFound($"Batch with ID {id} not found");

            return Ok(batch);
        }

        /// <summary>
        /// Create a new batch
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(BatchDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BatchDto>> Create([FromBody] CreateBatchDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var batch = await _batchService.CreateAsync(dto, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = batch.Id }, batch);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Update an existing batch
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(BatchDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BatchDto>> Update(Guid id, [FromBody] UpdateBatchDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var batch = await _batchService.UpdateAsync(id, dto, cancellationToken);
                return Ok(batch);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Delete a batch
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                await _batchService.DeleteAsync(id, cancellationToken);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
