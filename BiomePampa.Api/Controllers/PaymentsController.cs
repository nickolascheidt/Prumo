using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiomePampa.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>
        /// Obter pagamento por ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var payment = await _paymentService.GetByIdAsync(id, cancellationToken);
            if (payment == null)
                return NotFound();

            return Ok(payment);
        }

        /// <summary>
        /// Obter pagamentos por funcionário
        /// </summary>
        [HttpGet("employee/{employeeId}")]
        [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PaymentDto>>> GetByEmployeeId(Guid employeeId, CancellationToken cancellationToken)
        {
            var payments = await _paymentService.GetByEmployeeIdAsync(employeeId, cancellationToken);
            return Ok(payments);
        }

        /// <summary>
        /// Obter pagamentos por período
        /// </summary>
        [HttpGet("period")]
        [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PaymentDto>>> GetByPeriod(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            CancellationToken cancellationToken)
        {
            var payments = await _paymentService.GetByPeriodAsync(startDate, endDate, cancellationToken);
            return Ok(payments);
        }

        /// <summary>
        /// Obter pagamentos recentes
        /// </summary>
        [HttpGet("recent")]
        [ProducesResponseType(typeof(IEnumerable<PaymentSummaryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PaymentSummaryDto>>> GetRecent([FromQuery] int count = 10, CancellationToken cancellationToken = default)
        {
            var payments = await _paymentService.GetRecentPaymentsAsync(count, cancellationToken);
            return Ok(payments);
        }

        /// <summary>
        /// Registrar pagamento
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentDto>> Create([FromBody] CreatePaymentDto dto, CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            try
            {
                var payment = await _paymentService.CreateAsync(dto, userId, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = payment.Id }, payment);
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
        /// Excluir pagamento
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var result = await _paymentService.DeleteAsync(id, cancellationToken);
            if (!result)
                return NotFound();

            return NoContent();
        }
    }
}
