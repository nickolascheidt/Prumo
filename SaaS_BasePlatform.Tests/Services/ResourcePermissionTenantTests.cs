using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Common;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;
using Xunit;

namespace SaaS_BasePlatform.Tests.Services
{
    public class ResourcePermissionTenantTests
    {
        private static ApplicationDbContext NewDb(ITenantContext ctx) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, ctx);

        private static UserManager<ApplicationUser> MockUserManager(Guid userId)
        {
            var store = Substitute.For<IUserStore<ApplicationUser>>();
            var um = Substitute.For<UserManager<ApplicationUser>>(
                store, null, null, null, null, null, null, null, null);

            var user = new ApplicationUser { Id = userId, UserName = "test@example.com", Email = "test@example.com" };
            um.FindByIdAsync(userId.ToString()).Returns(user);
            um.GetRolesAsync(Arg.Is<ApplicationUser>(u => u.Id == userId))
              .Returns(new List<string>());

            return um;
        }

        [Fact]
        public async Task Tenant_owner_has_full_access_within_tenant()
        {
            var tenant = Guid.NewGuid();
            var ctx = Substitute.For<ITenantContext>();
            ctx.TenantId.Returns(tenant);
            ctx.HasTenant.Returns(true);
            using var db = NewDb(ctx);

            var user = Guid.NewGuid();
            db.Resources.Add(new Resource { Id = Guid.NewGuid(), TenantId = tenant, Code = "HR.Employees", Name = "x", Module = "RH", IsActive = true });
            db.TenantUsers.Add(new TenantUser { TenantId = tenant, UserId = user, Role = TenantRole.Owner });
            await db.SaveChangesAsync();

            var sut = new ResourcePermissionService(db, MockUserManager(user), ctx);
            var level = await sut.GetUserPermissionForResourceAsync(user, "HR.Employees");

            Assert.Equal(PermissionLevel.Full, level);
        }
    }
}
