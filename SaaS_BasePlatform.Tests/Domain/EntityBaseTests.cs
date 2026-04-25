using SaaS_BasePlatform.Domain.Common;

namespace SaaS_BasePlatform.Tests.Domain;

public class EntityBaseTests
{
    private class TestEntity : EntityBase { }

    [Fact]
    public void Constructor_ShouldGenerateNewId()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void Constructor_ShouldSetCreatedAtToUtcNow()
    {
        var before = DateTime.UtcNow;
        var entity = new TestEntity();
        var after = DateTime.UtcNow;

        Assert.InRange(entity.CreatedAt, before, after);
    }

    [Fact]
    public void Constructor_ShouldSetIsActiveToTrue()
    {
        var entity = new TestEntity();

        Assert.True(entity.IsActive);
    }

    [Fact]
    public void Constructor_ShouldSetUpdatedAtToNull()
    {
        var entity = new TestEntity();

        Assert.Null(entity.UpdatedAt);
    }

    [Fact]
    public void TwoEntities_ShouldHaveDifferentIds()
    {
        var entity1 = new TestEntity();
        var entity2 = new TestEntity();

        Assert.NotEqual(entity1.Id, entity2.Id);
    }
}
