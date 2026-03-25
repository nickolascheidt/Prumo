using BiomePampa.Api.Attributes;
using BiomePampa.Domain.Authorization;
using BiomePampa.Domain.DTOs;
using BiomePampa.Domain.Enums;
using BiomePampa.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiomePampa.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PermissionsController : ControllerBase
    {
        private readonly IPermissionService _permissionService;
        private readonly ILogger<PermissionsController> _logger;

        public PermissionsController(
            IPermissionService permissionService,
            ILogger<PermissionsController> logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        /// <summary>
        /// Listar todas as permissões disponíveis no sistema
        /// </summary>
        [HttpGet]
        [RequireResourceAccess("permissions", PermissionLevel.Read)]
        [ProducesResponseType(typeof(IEnumerable<PermissionDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PermissionDto>>> GetAllPermissions(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Buscando todas as permissões disponíveis");

            var permissions = await _permissionService.GetAllPermissionsAsync(cancellationToken);

            var permissionDtos = permissions.Select(p => new PermissionDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description
            });

            _logger.LogInformation("Retornadas {Count} permissões", permissionDtos.Count());

            return Ok(permissionDtos);
        }

        /// <summary>
        /// Obter permissões de uma role específica
        /// </summary>
        [HttpGet("roles/{roleName}")]
        [RequireResourceAccess("permissions", PermissionLevel.Read)]
        [ProducesResponseType(typeof(RolePermissionsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RolePermissionsDto>> GetRolePermissions(
            string roleName, 
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Buscando permissões da role: {RoleName}", roleName);

            try
            {
                var result = await _permissionService.GetRolePermissionsDetailedAsync(roleName, cancellationToken);

                _logger.LogInformation("Permissões da role {RoleName} retornadas com sucesso", roleName);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Role não encontrada: {RoleName}", roleName);
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Conceder permissão a uma role
        /// </summary>
        [HttpPost("roles/{roleName}/grant")]
        [RequireResourceAccess("permissions", PermissionLevel.Full)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GrantPermission(
            string roleName,
            [FromBody] GrantPermissionDto request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var userEmail = GetCurrentUserEmail();

            if (userId == Guid.Empty || string.IsNullOrEmpty(userEmail))
            {
                _logger.LogWarning("Tentativa de concessão de permissão sem usuário autenticado");
                return Unauthorized();
            }

            _logger.LogInformation(
                "Usuário {UserEmail} concedendo permissão {PermissionName} à role {RoleName}. Razão: {Reason}",
                userEmail, request.PermissionName, roleName, request.Reason);

            try
            {
                await _permissionService.GrantPermissionToRoleAsync(
                    roleName, 
                    request.PermissionName, 
                    userId, 
                    userEmail,
                    request.Reason,
                    cancellationToken);

                _logger.LogInformation(
                    "Permissão {PermissionName} concedida à role {RoleName} por {UserEmail}",
                    request.PermissionName, roleName, userEmail);

                return Ok(new { message = $"Permissão '{request.PermissionName}' concedida à role '{roleName}' com sucesso" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, 
                    "Erro ao conceder permissão {PermissionName} à role {RoleName}",
                    request.PermissionName, roleName);

                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Revogar permissão de uma role
        /// </summary>
        [HttpDelete("roles/{roleName}/revoke/{permissionName}")]
        [RequireResourceAccess("permissions", PermissionLevel.Full)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RevokePermission(
            string roleName,
            string permissionName,
            [FromQuery] string? reason,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            var userEmail = GetCurrentUserEmail();

            if (userId == Guid.Empty || string.IsNullOrEmpty(userEmail))
            {
                _logger.LogWarning("Tentativa de revogação de permissão sem usuário autenticado");
                return Unauthorized();
            }

            _logger.LogInformation(
                "Usuário {UserEmail} revogando permissão {PermissionName} da role {RoleName}. Razão: {Reason}",
                userEmail, permissionName, roleName, reason);

            try
            {
                await _permissionService.RevokePermissionFromRoleAsync(
                    roleName, 
                    permissionName, 
                    userId, 
                    userEmail,
                    reason,
                    cancellationToken);

                _logger.LogInformation(
                    "Permissão {PermissionName} revogada da role {RoleName} por {UserEmail}",
                    permissionName, roleName, userEmail);

                return Ok(new { message = $"Permissão '{permissionName}' revogada da role '{roleName}' com sucesso" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex,
                    "Erro ao revogar permissão {PermissionName} da role {RoleName}",
                    permissionName, roleName);

                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Obter histórico de auditoria de mudanças em permissões
        /// </summary>
        [HttpGet("audit")]
        [RequireResourceAccess("permissions", PermissionLevel.Read)]
        [ProducesResponseType(typeof(IEnumerable<PermissionAuditDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PermissionAuditDto>>> GetAuditLogs(
            [FromQuery] string? roleName = null,
            [FromQuery] int take = 100,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Buscando logs de auditoria. RoleName: {RoleName}, Take: {Take}",
                roleName ?? "Todas", take);

            var logs = await _permissionService.GetAuditLogsAsync(roleName, take, cancellationToken);

            return Ok(logs);
        }

        /// <summary>
        /// Obter catálogo de permissões disponíveis (constantes do sistema)
        /// </summary>
        [HttpGet("catalog")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public ActionResult GetPermissionsCatalog()
        {
            var catalog = new
            {
                employees = new
                {
                    all = Permissions.Employees.All,
                    view = Permissions.Employees.View,
                    create = Permissions.Employees.Create,
                    edit = Permissions.Employees.Edit,
                    delete = Permissions.Employees.Delete,
                    managePayments = Permissions.Employees.ManagePayments
                },
                worklogs = new
                {
                    all = Permissions.WorkLogs.All,
                    view = Permissions.WorkLogs.View,
                    create = Permissions.WorkLogs.Create,
                    edit = Permissions.WorkLogs.Edit,
                    delete = Permissions.WorkLogs.Delete
                },
                payments = new
                {
                    all = Permissions.Payments.All,
                    view = Permissions.Payments.View,
                    create = Permissions.Payments.Create,
                    delete = Permissions.Payments.Delete,
                    viewReports = Permissions.Payments.ViewReports
                },
                products = new
                {
                    all = Permissions.Products.All,
                    view = Permissions.Products.View,
                    create = Permissions.Products.Create,
                    edit = Permissions.Products.Edit,
                    delete = Permissions.Products.Delete
                },
                customers = new
                {
                    all = Permissions.Customers.All,
                    view = Permissions.Customers.View,
                    create = Permissions.Customers.Create,
                    edit = Permissions.Customers.Edit,
                    delete = Permissions.Customers.Delete
                },
                stock = new
                {
                    all = Permissions.Stock.All,
                    view = Permissions.Stock.View,
                    manage = Permissions.Stock.Manage,
                    viewReports = Permissions.Stock.ViewReports
                }
            };

            return Ok(catalog);
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        private string GetCurrentUserEmail()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        }
    }
}
