using Microsoft.AspNetCore.Identity;

namespace Prumo.Domain.Entities
{
    public class ApplicationRole : IdentityRole<Guid>
    {
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tenant dono desta role. <c>null</c> = role canônica do sistema (Administrador,
        /// RH, Financeiro, ContasAPagar, Funcionario, Cliente), visível em todos os
        /// tenants. Preenchido = criada por aquele tenant e visível só nele.
        /// </summary>
        /// <remarks>
        /// Deliberadamente <b>não</b> implementa <c>ITenantScoped</c>: o filtro global é
        /// fail-closed e esconderia as canônicas (TenantId null) de todo mundo, inclusive
        /// do login. A filtragem por tenant é explícita nas listagens.
        /// </remarks>
        public Guid? TenantId { get; set; }
    }
}
