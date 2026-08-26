using Prumo.Domain.Common;
using Prumo.Domain.Enums;

namespace Prumo.Domain.Entities
{
    /// <summary>
    /// Histórico de mudanças de nível de acesso: quem deu ou tirou o quê, de quem, quando.
    /// </summary>
    /// <remarks>
    /// Substitui o <c>PermissionAuditLog</c>, que auditava o sistema de
    /// <c>RolePermission</c> — desconectado de qualquer gate e aposentado no item 3B.
    /// Este audita <c>ResourcePermission</c>, que é o que de fato controla o menu, os
    /// guards e a API.
    ///
    /// Guarda <b>nomes</b> além dos ids de propósito: excluir uma role apaga suas
    /// permissões, e sem o nome copiado aqui o histórico viraria uma lista de GUIDs
    /// órfãos — justamente quando se quer saber o que aconteceu.
    /// </remarks>
    public class ResourcePermissionAuditLog : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }

        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = null!;

        public Guid ResourceId { get; set; }
        public string ResourceCode { get; set; } = null!;

        /// <summary>Nível antes da mudança. <c>None</c> quando o acesso não existia.</summary>
        public PermissionLevel PreviousLevel { get; set; }

        /// <summary>Nível depois da mudança. <c>None</c> quando foi revogado.</summary>
        public PermissionLevel NewLevel { get; set; }

        public Guid PerformedByUserId { get; set; }
        public string PerformedByUserEmail { get; set; } = null!;

        public DateTime PerformedAt { get; set; }
    }
}
