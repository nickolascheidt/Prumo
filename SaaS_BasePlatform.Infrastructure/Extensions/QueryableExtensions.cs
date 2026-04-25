using SaaS_BasePlatform.Domain.Common;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace SaaS_BasePlatform.Infrastructure.Extensions
{
    public static class QueryableExtensions
    {
        public static async Task<PagedResult<T>> ToPagedListAsync<T>(
            this IQueryable<T> source,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var count = await source.CountAsync(cancellationToken);
            var items = await source
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<T>(items, count, pageNumber, pageSize);
        }

        public static IQueryable<T> ApplyPaging<T>(
            this IQueryable<T> source,
            PaginationParameters parameters)
        {
            return source
                .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                .Take(parameters.PageSize);
        }

        public static IQueryable<T> ApplySorting<T>(
            this IQueryable<T> source,
            string? sortBy,
            bool descending = false)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
                return source;

            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, sortBy);
            var lambda = Expression.Lambda(property, parameter);

            var methodName = descending ? "OrderByDescending" : "OrderBy";
            var resultExpression = Expression.Call(
                typeof(Queryable),
                methodName,
                new Type[] { typeof(T), property.Type },
                source.Expression,
                Expression.Quote(lambda));

            return source.Provider.CreateQuery<T>(resultExpression);
        }
    }
}
