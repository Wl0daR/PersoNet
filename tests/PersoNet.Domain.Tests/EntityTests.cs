using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Tests;

public sealed class EntityTests
{
    [Fact]
    public void New_entity_gets_version_7_id()
    {
        var entity = new TestEntity();

        entity.Id.Version.ShouldBe(7);
    }

    [Fact]
    public void Each_entity_gets_its_own_id()
    {
        var first = new TestEntity();
        var second = new TestEntity();

        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public void Empty_id_is_rejected()
    {
        Should.Throw<ArgumentException>(() => new TestEntity(Guid.Empty));
    }

    private sealed class TestEntity : Entity
    {
        public TestEntity()
        {
        }

        public TestEntity(Guid id)
            : base(id)
        {
        }
    }
}
