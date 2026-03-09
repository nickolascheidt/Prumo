using BiomePampa.Application.DTOs.Employee;
using BiomePampa.Application.Services;
using BiomePampa.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiomePampa.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EmployeesController : ControllerBase
    {
        private readonly IEmployeeService _employeeService;

        public EmployeesController(IEmployeeService employeeService)
        {
            _employeeService = employeeService;
        }

        /// <summary>
        /// Listar todos os funcionários
        /// </summary>
        [HttpGet]
        [Authorize(Policy = Permissions.Employees.View)]
        [ProducesResponseType(typeof(IEnumerable<EmployeeDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetAll([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var employees = await _employeeService.GetAllAsync(includeInactive, cancellationToken);
            return Ok(employees);
        }

        /// <summary>
        /// Listar resumo dos funcionários
        /// </summary>
        [HttpGet("summaries")]
        [ProducesResponseType(typeof(IEnumerable<EmployeeSummaryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<EmployeeSummaryDto>>> GetSummaries([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            var summaries = await _employeeService.GetSummariesAsync(includeInactive, cancellationToken);
            return Ok(summaries);
        }

        /// <summary>
        /// Obter funcionário por ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EmployeeDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var employee = await _employeeService.GetByIdAsync(id, cancellationToken);
            if (employee == null)
                return NotFound();

            return Ok(employee);
        }

        /// <summary>
        /// Obter funcionário por CPF
        /// </summary>
        [HttpGet("cpf/{cpf}")]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EmployeeDto>> GetByCPF(string cpf, CancellationToken cancellationToken)
        {
            var employee = await _employeeService.GetByCPFAsync(cpf, cancellationToken);
            if (employee == null)
                return NotFound();

            return Ok(employee);
        }

        /// <summary>
        /// Cadastrar novo funcionário
        /// </summary>
        [HttpPost]
        [Authorize(Policy = Permissions.Employees.Create)]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<EmployeeDto>> Create([FromBody] CreateEmployeeDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var employee = await _employeeService.CreateAsync(dto, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { id = employee.Id }, employee);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Atualizar dados do funcionário
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Policy = Permissions.Employees.Edit)]
        [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<EmployeeDto>> Update(Guid id, [FromBody] UpdateEmployeeDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var employee = await _employeeService.UpdateAsync(id, dto, cancellationToken);
                return Ok(employee);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Desativar funcionário
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Policy = Permissions.Employees.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var result = await _employeeService.DeleteAsync(id, cancellationToken);
            if (!result)
                return NotFound();

            return NoContent();
        }
    }
}
