using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Infrastructure.Data;

namespace SaaS_BasePlatform.Infrastructure.Repositories
{
    public interface IUnitOfWork : IDisposable, IAsyncDisposable
    {
        IRepository<T> Repository<T>() where T : EntityBase;
        ApplicationDbContext Context { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}
