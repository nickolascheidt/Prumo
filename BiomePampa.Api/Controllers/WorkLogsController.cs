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
    public class WorkLogsController : ControllerBase
    {
        private readonly IWorkLogService _workLogService;

        public WorkLogsController(IWorkLogService workLogService)
        {
            _workLogService = workLogService;
        }

        /// <summary>
        /// Obter registro de horas do mês atual
        /// </summary>
        [HttpGet()]
        [RequireResourceAccess("work-logs", PermissionLevel.Read)]
        [ProducesResponseType(typeof(IEnumerable<WorkLogDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<WorkLogDto>>> GetCurrentMonth(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            CancellationToken cancellationToken)
        {
            var workLogs = await _workLogService.GetCurrentMonthAsync(startDate, endDate, cancellationToken);
            return Ok(workLogs);
        }

        /// <summary>
        /// Obter registro de horas por ID
        /// </summary>
        [HttpGet("{id}")]
        [RequireResourceAccess("work-logs", PermissionLevel.Read)]
        [ProducesResponseType(typeof(WorkLogDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<WorkLogDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var workLog = await _workLogService.GetByIdAsync(id, cancellationToken);
            if (workLog == null)
                return NotFound();

            return Ok(workLog);
        }

        /// <summary>
        /// Obter registros de horas por funcionário
        /// </summary>
        [HttpGet("employee/{employeeId}")]
        [RequireResourceAccess("work-logs", PermissionLevel.Read)]
        [ProducesResponseType(typeof(IEnumerable<WorkLogDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<WorkLogDto>>> GetByEmployeeId(
            Guid employeeId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            CancellationToken cancellationToken = default)
        {
            var workLogs = await _workLogService.GetByEmployeeIdAsync(employeeId, startDate, endDate, cancellationToken);
            return Ok(workLogs);
        }

        /// <summary>
        /// Obter registros de horas não atribuídos a períodos
        /// </summary>
        [HttpGet("employee/{employeeId}/unassigned")]
        [RequireResourceAccess("work-logs", PermissionLevel.Read)]
        [ProducesResponseType(typeof(IEnumerable<WorkLogDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<WorkLogDto>>> GetUnassigned(Guid employeeId, CancellationToken cancellationToken)
        {
            var workLogs = await _workLogService.GetUnassignedAsync(employeeId, cancellationToken);
            return Ok(workLogs);
        }

        /// <summary>
        /// Registrar horas trabalhadas
        /// </summary>
        [HttpPost]
        [RequireResourceAccess("work-logs", PermissionLevel.Write)]
        [ProducesResponseType(typeof(WorkLogDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<WorkLogDto>> Create([FromBody] CreateWorkLogDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var workLog = await _workLogService.CreateAsync(dto, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = workLog.Id }, workLog);
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
        /// Atualizar registro de horas
        /// </summary>
        [HttpPut("{id}")]
        [RequireResourceAccess("work-logs", PermissionLevel.Write)]
        [ProducesResponseType(typeof(WorkLogDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<WorkLogDto>> Update(Guid id, [FromBody] UpdateWorkLogDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var workLog = await _workLogService.UpdateAsync(id, dto, cancellationToken);
                return Ok(workLog);
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
        /// Excluir registro de horas
        /// </summary>
        [HttpDelete("{id}")]
        [RequireResourceAccess("work-logs", PermissionLevel.Full)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _workLogService.DeleteAsync(id, cancellationToken);
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
