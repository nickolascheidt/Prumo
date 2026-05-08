using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SaaS_BasePlatform.Application.DTOs.Tenants;
using SaaS_BasePlatform.Application.Services;
using SaaS_BasePlatform.Domain.Entities;
using SaaS_BasePlatform.Domain.Enums;
using SaaS_BasePlatform.Infrastructure.Data;

namespace SaaS_BasePlatform.Tests.Services;

public class TenantServiceCreateUserTests
{
    private static ApplicationDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UserManager<ApplicationUser> MakeUserManager()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        return Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    [Fact]
    public async Task CreateAndAddMemberAsync_WhenEmailAlreadyExists_ThrowsInvalidOperation()
    {
        var db = MakeDb();
        var userManager = MakeUserManager();
        var existingUser = new ApplicationUser { Email = "taken@example.com", UserName = "taken@example.com" };
        userManager.FindByEmailAsync("taken@example.com").Returns(existingUser);

        var sut = new TenantService(db, userManager);
        var dto = new CreateTenantUserDto("taken@example.com", "Pass1!", "Test User", null, TenantRole.Member);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAndAddMemberAsync(Guid.NewGuid(), dto));
    }

    [Fact]
    public async Task CreateAndAddMemberAsync_WhenIdentityFails_ThrowsInvalidOperation()
    {
        var db = MakeDb();
        var userManager = MakeUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Too weak" }));

        var sut = new TenantService(db, userManager);
        var dto = new CreateTenantUserDto("new@example.com", "weak", "Test User", null, TenantRole.Member);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAndAddMemberAsync(Guid.NewGuid(), dto));
        Assert.Contains("Too weak", ex.Message);
    }

    [Fact]
    public async Task CreateAndAddMemberAsync_WhenRoleAssignmentFails_ThrowsInvalidOperation()
    {
        var db = MakeDb();
        var userManager = MakeUserManager();
        userManager.FindByEmailAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        userManager.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                var user = callInfo.ArgAt<ApplicationUser>(0);
                db.Users.Add(user);
                db.SaveChanges();
                return IdentityResult.Success;
            });
        userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), "Usuario")
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Role not found" }));
        userManager.DeleteAsync(Arg.Any<ApplicationUser>())
            .Returns(callInfo =>
            {
                var user = callInfo.ArgAt<ApplicationUser>(0);
                db.Users.Remove(user);
                db.SaveChanges();
                return IdentityResult.Success;
            });

        var sut = new TenantService(db, userManager);
        var dto = new CreateTenantUserDto("new@example.com", "Pass1!", "Test User", null, TenantRole.Member);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CreateAndAddMemberAsync(Guid.NewGuid(), dto));
        Assert.Contains("Role not found", ex.Message);
        Assert.False(await db.Users.AnyAsync(u => u.Email == "new@example.com"));
    }

    [Fact]
    public async Task CreateAndAddMemberAsync_OnSuccess_ReturnsMemberDtoAndAddsTenantUser()
    {
        var db = MakeDb();
        var userManager = MakeUserManager();
        var tenantId = Guid.NewGuid();

        // Seed tenant so FK constraint is satisfied
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Acme", Slug = "acme", OwnerUserId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        userManager.FindByEmailAsync("new@example.com").Returns((ApplicationUser?)null);
        userManager.CreateAsync(Arg.Any<ApplicationUser>(), "Pass1!")
            .Returns(callInfo =>
            {
                // Simulate Identity persisting the user by adding it to the DB
                var user = callInfo.ArgAt<ApplicationUser>(0);
                db.Users.Add(user);
                db.SaveChanges();
                return IdentityResult.Success;
            });
        userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), "Usuario")
            .Returns(IdentityResult.Success);

        var sut = new TenantService(db, userManager);
        var dto = new CreateTenantUserDto("new@example.com", "Pass1!", "João Silva", "+55 11 99999-0000", TenantRole.Member);

        var result = await sut.CreateAndAddMemberAsync(tenantId, dto);

        Assert.Equal("new@example.com", result.Email);
        Assert.Equal("João Silva", result.FullName);
        Assert.Equal(TenantRole.Member, result.Role);
        Assert.True(await db.TenantUsers.AnyAsync(tu =>
            tu.TenantId == tenantId && tu.Role == TenantRole.Member));
        await userManager.Received(1).AddToRoleAsync(Arg.Any<ApplicationUser>(), "Usuario");
    }
}
