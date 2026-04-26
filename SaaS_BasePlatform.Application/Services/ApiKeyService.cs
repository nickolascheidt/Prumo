using Microsoft.EntityFrameworkCore;
using SaaS_BasePlatform.Application.DTOs.ApiKeys;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;
using SaaS_BasePlatform.Infrastructure.Security;

namespace SaaS_BasePlatform.Application.Services
{
    public class ApiKeyService : IApiKeyService
    {
        private readonly ApplicationDbContext _db;

        public ApiKeyService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<CreateApiKeyResponseDto> CreateAsync(Guid tenantId, Guid createdByUserId, CreateApiKeyRequestDto request, CancellationToken cancellationToken = default)
        {
            var (fullKey, prefix, hash) = ApiKeyHasher.Generate(request.Type == ApiKeyType.Service);

            var entity = new ApiKey
            {
                TenantId = tenantId,
                Name = request.Name,
                Type = request.Type,
                Prefix = prefix,
                KeyHash = hash,
                ExpiresAt = request.ExpiresAt,
                CreatedByUserId = createdByUserId
            };

            _db.ApiKeys.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            return new CreateApiKeyResponseDto(
                entity.Id,
                entity.Name,
                entity.Type,
                fullKey,
                entity.Prefix,
                entity.ExpiresAt,
                entity.CreatedAt);
        }

        public async Task<IReadOnlyList<ApiKeyDto>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return await _db.ApiKeys
                .IgnoreQueryFilters()
                .Where(k => k.TenantId == tenantId)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new ApiKeyDto(
                    k.Id,
                    k.Name,
                    k.Type,
                    k.Prefix,
                    k.CreatedAt,
                    k.ExpiresAt,
                    k.RevokedAt,
                    k.LastUsedAt))
                .ToListAsync(cancellationToken);
        }

        public async Task RevokeAsync(Guid tenantId, Guid apiKeyId, CancellationToken cancellationToken = default)
        {
            var key = await _db.ApiKeys
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(k => k.Id == apiKeyId && k.TenantId == tenantId, cancellationToken);
            if (key == null)
                throw new KeyNotFoundException("API key not found");

            if (key.RevokedAt != null) return;

            key.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<ApiKey?> ValidateAsync(string presentedKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(presentedKey)) return null;
            if (!ApiKeyHasher.TryGetPrefix(presentedKey, out _)) return null;

            var hash = ApiKeyHasher.Hash(presentedKey);

            var key = await _db.ApiKeys
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(k => k.KeyHash == hash, cancellationToken);

            if (key == null) return null;
            if (key.RevokedAt != null) return null;
            if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTime.UtcNow) return null;

            key.LastUsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return key;
        }
    }
}
