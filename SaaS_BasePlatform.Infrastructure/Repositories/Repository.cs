using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace SaaS_BasePlatform.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : EntityBase
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(e => e.IsActive).ToListAsync(cancellationToken);
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
        }

        public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddAsync(entity, cancellationToken);
            return entity;
        }

        public virtual void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public virtual void Delete(T entity)
        {
            entity.IsActive = false;
            _dbSet.Update(entity);
        }

        public virtual async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return false;

            Delete(entity);
            return true;
        }

        public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AnyAsync(e => e.Id == id, cancellationToken);
        }
    }
}
