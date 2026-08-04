using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.VisionTests;

/// <summary>
/// Named "VisionTest" (not "Test") to avoid colliding with the DotGlasses.Application.Tests
/// xUnit project's own root namespace — the Domain entity itself is still Test
/// (DotGlasses.Domain.Entities.Test).
///
/// Add/Update only track changes (no auto-save) — the calling service persists via IUnitOfWork,
/// letting LeadService batch "create this Lead and set Test.ConvertedToLeadId" into one
/// transaction. See CLAUDE.md's Test/Lead/Sale API section.
/// </summary>
public interface IVisionTestRepository
{
    Task<Test?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Test>> ListAsync(CancellationToken cancellationToken = default);
    void Add(Test entity);
    void Update(Test entity);
}
