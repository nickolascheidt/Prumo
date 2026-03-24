using BiomePampa.Api.Attributes;
using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Application.Services;
using BiomePampa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiomePampa.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentPeriodsController : ControllerBase
    {
        private readonly IPaymentPeriodService _paymentPeriodService;

        public PaymentPeriodsController(IPaymentPeriodService paymentPeriodService)
        {
            _paymentPeriodService = paymentPeriodService;
        }

        /// <summary>
        /// Obter período de pagamento por ID
        /// </summary>
        [HttpGet("{id}")]
        [RequireResourceAccess("payment-periods", PermissionLevel.Read)]
        [ProducesResponseType(typeof(PaymentPeriodDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentPeriodDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var paymentPeriod = await _paymentPeriodService.GetByIdAsync(id, cancellationToken);
            if (paymentPeriod == null)
                return NotFound();

            return Ok(paymentPeriod);
        }

        /// <summary>
        /// Obter períodos de pagamento por funcionário
        /// </summary>
        [HttpGet("employee/{employeeId}")]
        [RequireResourceAccess("payment-periods", PermissionLevel.Read)]
        [ProducesResponseType(typeof(IEnumerable<PaymentPeriodDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PaymentPeriodDto>>> GetByEmployeeId(Guid employeeId, CancellationToken cancellationToken)
        {
            var paymentPeriods = await _paymentPeriodService.GetByEmployeeIdAsync(employeeId, cancellationToken);
            return Ok(paymentPeriods);
        }

        /// <summary>
        /// Obter períodos de pagamento por status
        /// </summary>
        [HttpGet("status/{status}")]
        [RequireResourceAccess("payment-periods", PermissionLevel.Read)]
        [ProducesResponseType(typeof(IEnumerable<PaymentPeriodSummaryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PaymentPeriodSummaryDto>>> GetByStatus(PaymentStatus status, CancellationToken cancellationToken)
        {
            var paymentPeriods = await _paymentPeriodService.GetByStatusAsync(status, cancellationToken);
            return Ok(paymentPeriods);
        }

        /// <summary>
        /// Criar período de pagamento
        /// </summary>
        [HttpPost]
        [RequireResourceAccess("payment-periods", PermissionLevel.Write)]
        [ProducesResponseType(typeof(PaymentPeriodDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentPeriodDto>> Create([FromBody] CreatePaymentPeriodDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var paymentPeriod = await _paymentPeriodService.CreateAsync(dto, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = paymentPeriod.Id }, paymentPeriod);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gerar período de pagamento automaticamente para um funcionário
        /// </summary>
        [HttpPost("generate")]
        [RequireResourceAccess("payment-periods", PermissionLevel.Write)]
        [ProducesResponseType(typeof(PaymentPeriodDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentPeriodDto>> Generate(
            [FromQuery] Guid employeeId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var paymentPeriod = await _paymentPeriodService.GenerateForEmployeeAsync(employeeId, startDate, endDate, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = paymentPeriod.Id }, paymentPeriod);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Atualizar status do período
        /// </summary>
        [HttpPatch("{id}/status")]
        [RequireResourceAccess("payment-periods", PermissionLevel.Write)]
        [ProducesResponseType(typeof(PaymentPeriodDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentPeriodDto>> UpdateStatus(Guid id, [FromBody] PaymentStatus status, CancellationToken cancellationToken)
        {
            try
            {
                var paymentPeriod = await _paymentPeriodService.UpdateStatusAsync(id, status, cancellationToken);
                return Ok(paymentPeriod);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Excluir período de pagamento
        /// </summary>
        [HttpDelete("{id}")]
        [RequireResourceAccess("payment-periods", PermissionLevel.Full)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _paymentPeriodService.DeleteAsync(id, cancellationToken);
                if (!result)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
