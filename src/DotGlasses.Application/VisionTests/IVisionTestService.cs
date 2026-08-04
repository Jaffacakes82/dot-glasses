using DotGlasses.Contracts.Tests;

namespace DotGlasses.Application.VisionTests;

public interface IVisionTestService
{
    Task<TestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TestDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Idempotent upsert keyed on <see cref="CreateTestRequest.Id"/> — a replayed
    /// offline-sync create for a record that already exists is a no-op, returning the existing
    /// record rather than overwriting it. hierarchyPath/technicianUserId come from the
    /// authenticated caller, not the request body.</summary>
    Task<TestDto> CreateAsync(CreateTestRequest request, Guid technicianUserId, string hierarchyPath, CancellationToken cancellationToken = default);
}
