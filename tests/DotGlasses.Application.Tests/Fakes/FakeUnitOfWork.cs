using DotGlasses.Application.Common;

namespace DotGlasses.Application.Tests.Fakes;

/// <summary>
/// Counts commits. The services under test batch a create and its back-link write into a single
/// <see cref="IUnitOfWork.SaveChangesAsync"/> call so the pair commits atomically; a test can
/// assert that guarantee here without a database. A real transaction can only be exercised
/// against real Postgres — that lives in the integration suite, not this one.
/// </summary>
public class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(0);
    }
}
