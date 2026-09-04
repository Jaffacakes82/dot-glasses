using DotGlasses.Application.VisionTests;
using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Tests.Fakes;

/// <summary>
/// Dictionary-backed stand-in for the EF repository, following
/// <c>FakeWidgetExampleRepository</c>. Reads honour <see cref="HideFromCaller"/>, which models
/// the global hierarchy query filter: a row outside the caller's subtree is simply not returned,
/// while still existing in the store — so a test can assert what did (or did not) happen to a row
/// the caller could not see.
/// </summary>
public class FakeVisionTestRepository : IVisionTestRepository
{
    private readonly Dictionary<Guid, Test> _store = [];
    private readonly HashSet<Guid> _hidden = [];

    public void Seed(Test entity) => _store[entity.Id] = entity;

    /// <summary>Puts an existing row outside the caller's hierarchy scope.</summary>
    public void HideFromCaller(Guid id) => _hidden.Add(id);

    /// <summary>Reads a row regardless of scope — for assertions only, never for the service.</summary>
    public Test? Inspect(Guid id) => _store.GetValueOrDefault(id);

    public int Count => _store.Count;

    public Task<Test?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_hidden.Contains(id) ? null : _store.GetValueOrDefault(id));

    public Task<IReadOnlyList<Test>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Test>>(_store.Values.Where(x => !_hidden.Contains(x.Id)).ToList());

    public void Add(Test entity) => _store[entity.Id] = entity;

    public void Update(Test entity) => _store[entity.Id] = entity;
}
