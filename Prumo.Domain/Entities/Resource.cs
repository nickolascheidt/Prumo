using Prumo.Domain.Common;

namespace Prumo.Domain.Entities
{
    /// <summary>
    /// Representa um recurso da aplicação (tela, funcionalidade, módulo)
    /// Ex: "WorkLog.Management", "Employee.List", "Reports.Financial"
    /// </summary>
    public class Resource : EntityBase, ITenantScoped
    {
        public Guid TenantId { get; set; }

        /// <summary>
        /// Identificador único do recurso (ex: "WorkLog.Management")
        /// </summary>
        public string Code { get; set; } = null!;

        /// <summary>
        /// Nome amigável do recurso (ex: "Gestão de Ponto")
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Descrição do recurso
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Módulo/Área a que pertence (ex: "HR", "Finance", "Administration")
        /// </summary>
        public string? Module { get; set; }

        /// <summary>
        /// Rota no frontend (opcional, para facilitar navegação)
        /// </summary>
        public string? FrontendRoute { get; set; }

        /// <summary>
        /// Ícone para exibir no menu (opcional)
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Ordem de exibição no menu
        /// </summary>
        public int DisplayOrder { get; set; }

        // Relacionamentos
        public ICollection<ResourcePermission> ResourcePermissions { get; set; } = new List<ResourcePermission>();
    }
}
