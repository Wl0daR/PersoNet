using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Domain.Abstractions;
using PersoNet.Infrastructure.Persistence;
using PersoNet.Testing;

namespace PersoNet.Infrastructure.IntegrationTests;

/// <summary>
/// Proves the tenant filter in <see cref="PersoNetDbContext"/> against a real PostgreSQL, using a test-only tenant-owned entity.
/// </summary>
public sealed class TenantIsolationTests : IAsyncLifetime
{
    private static readonly string ConnectionString = TestDatabase.ConnectionStringFor("personet_infrastructure_tests");

    private readonly Guid _tenantA = Guid.CreateVersion7();
    private readonly Guid _tenantB = Guid.CreateVersion7();

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateContext(tenantId: null);
        await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Queries_return_only_current_tenant_rows()
    {
        await SeedAsync(_tenantA, "A1", "A2");
        await SeedAsync(_tenantB, "B1");

        await using var context = CreateContext(_tenantA);
        var titles = await context.Notes.Select(note => note.Title).OrderBy(title => title)
            .ToListAsync(TestContext.Current.CancellationToken);

        titles.ShouldBe(["A1", "A2"]);
    }

    [Fact]
    public async Task Without_tenant_no_tenant_owned_rows_are_visible()
    {
        await SeedAsync(_tenantA, "A1");

        await using var context = CreateContext(tenantId: null);

        (await context.Notes.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task Saving_another_tenants_row_is_rejected()
    {
        await using var context = CreateContext(_tenantA);
        context.Notes.Add(new TestNote(_tenantB, "B1"));

        await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static async Task SeedAsync(Guid tenantId, params string[] titles)
    {
        await using var context = CreateContext(tenantId);
        context.Notes.AddRange(titles.Select(title => new TestNote(tenantId, title)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static TestDbContext CreateContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>();
        options.UsePersoNetDatabase(ConnectionString);

        return new TestDbContext(options.Options, new FixedTenantContext(tenantId));
    }

    private sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class TestNote : Entity, ITenantOwned
    {
        public TestNote(Guid tenantId, string title)
        {
            TenantId = tenantId;
            Title = title;
        }

        public Guid TenantId { get; private set; }

        public string Title { get; private set; }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenantContext)
        : PersoNetDbContext(options, tenantContext)
    {
        public DbSet<TestNote> Notes => Set<TestNote>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestNote>(note =>
            {
                note.Property(n => n.Id).ValueGeneratedNever();
                note.Property(n => n.Title).HasMaxLength(100);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
