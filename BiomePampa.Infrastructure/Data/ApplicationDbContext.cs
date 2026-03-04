using BiomePampa.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BiomePampa.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Batch> Batches => Set<Batch>();
        public DbSet<Stock> Stocks => Set<Stock>();
        public DbSet<StockMovement> StockMovements => Set<StockMovement>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<WorkLog> WorkLogs => Set<WorkLog>();
        public DbSet<PaymentPeriod> PaymentPeriods => Set<PaymentPeriod>();
        public DbSet<Payment> Payments => Set<Payment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            // Seed Roles
            var adminRoleId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
            var userRoleId = Guid.Parse("a2222222-2222-2222-2222-222222222222");
            var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<ApplicationRole>().HasData(
                new ApplicationRole
                {
                    Id = adminRoleId,
                    Name = "Administrador",
                    NormalizedName = "ADMINISTRADOR",
                    Description = "Acesso total ao sistema",
                    CreatedAt = seedDate,
                    ConcurrencyStamp = "a1111111-1111-1111-1111-111111111111"
                },
                new ApplicationRole
                {
                    Id = userRoleId,
                    Name = "Usuario",
                    NormalizedName = "USUARIO",
                    Description = "Acesso limitado ao sistema",
                    CreatedAt = seedDate,
                    ConcurrencyStamp = "a2222222-2222-2222-2222-222222222222"
                }
            );
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<Domain.Common.EntityBase>())
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
