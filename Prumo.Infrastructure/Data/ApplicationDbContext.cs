using Prumo.Domain.Common;
using Prumo.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Prumo.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        private readonly ITenantContext? _tenantContext;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
            : base(options)
        {
            _tenantContext = tenantContext;
        }

        // DbSets para Controle de Acesso e Permissões
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<PermissionAuditLog> PermissionAuditLogs => Set<PermissionAuditLog>();

        // DbSets para Controle de Acesso Baseado em Recursos
        public DbSet<Resource> Resources => Set<Resource>();
        public DbSet<ResourcePermission> ResourcePermissions => Set<ResourcePermission>();

        // Multi-tenancy
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
        public DbSet<TenantUserRole> TenantUserRoles => Set<TenantUserRole>();

        // Accounts Payable (Contas a Pagar)
        public DbSet<AccountsPayableCategory> AccountsPayableCategories => Set<AccountsPayableCategory>();
        public DbSet<AccountsPayableEntry> AccountsPayableEntries => Set<AccountsPayableEntry>();

        // General Ledger
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
        public DbSet<JournalLine> JournalLines => Set<JournalLine>();
        public DbSet<TenantGlSettings> TenantGlSettings => Set<TenantGlSettings>();

        // HR Module
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<WorkLog> WorkLogs => Set<WorkLog>();
        public DbSet<PaymentPeriod> PaymentPeriods => Set<PaymentPeriod>();
        public DbSet<Payment> Payments => Set<Payment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            ApplyTenantQueryFilters(modelBuilder);
        }

        private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
                    continue;

                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetTenantQueryFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, new object[] { modelBuilder });
            }
        }

        private void SetTenantQueryFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantScoped
        {
            // Fail-closed: sem tenant resolvido não volta linha nenhuma. O construtor sem
            // ITenantContext (usado pelo ApplicationDbContextFactory em design-time) deixa
            // _tenantContext nulo e portanto filtra tudo — inofensivo, porque design-time só
            // roda migration e migration não faz query. Quem precisa ler cross-tenant de
            // propósito usa IgnoreQueryFilters() explicitamente.
            modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
                _tenantContext != null
                && _tenantContext.HasTenant
                && e.TenantId == _tenantContext.TenantId);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<EntityBase>())
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
            }

            if (_tenantContext != null && _tenantContext.HasTenant)
            {
                foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
                {
                    if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                    {
                        entry.Entity.TenantId = _tenantContext.TenantId!.Value;
                    }
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
