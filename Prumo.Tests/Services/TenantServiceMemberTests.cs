using NSubstitute;
using Prumo.Application.DTOs.Auth;
using Prumo.Application.DTOs.Tenants;
using Prumo.Application.Services;
using Prumo.Domain.Enums;

namespace Prumo.Tests.Services;

public class TenantServiceMemberTests
{
    private readonly ITenantService _service = Substitute.For<ITenantService>();

    [Fact]
    public async Task LookupUserByEmail_WhenUserExists_ReturnsDto()
    {
        var expected = new UserLookupDto(Guid.NewGuid(), "alice@example.com", "Alice");
        _service.LookupUserByEmailAsync("alice@example.com", Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _service.LookupUserByEmailAsync("alice@example.com");

        Assert.NotNull(result);
        Assert.Equal("alice@example.com", result!.Email);
    }

    [Fact]
    public async Task LookupUserByEmail_WhenUserNotFound_ReturnsNull()
    {
        _service.LookupUserByEmailAsync("ghost@example.com", Arg.Any<CancellationToken>())
            .Returns((UserLookupDto?)null);

        var result = await _service.LookupUserByEmailAsync("ghost@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateMemberRole_CallsService_WithCorrectArguments()
    {
        var tenantId = Guid.NewGuid();
        var userId   = Guid.NewGuid();

        await _service.UpdateMemberRoleAsync(tenantId, userId, TenantRole.Admin);

        await _service.Received(1).UpdateMemberRoleAsync(tenantId, userId, TenantRole.Admin, Arg.Any<CancellationToken>());
    }
}
