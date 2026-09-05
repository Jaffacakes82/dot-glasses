using DotGlasses.Application.Leads;
using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Tests.Fakes;

/// <summary>
/// Dictionary-backed stand-in for the EF repository. <see cref="HideFromCaller"/> models the
/// global hierarchy query filter — see <see cref="FakeVisionTestRepository"/>. Ordering mirrors
/// the real repository's "most recently created first", using an insertion counter in place of
/// the audit interceptor's CreatedAtUtc stamp.
/// </summary>
public class FakeLeadRepository : ILeadRepository
{
    private readonly Dictionary<Guid, Lead> _store = [];
    private readonly Dictionary<Guid, int> _insertionOrder = [];
    private readonly HashSet<Guid> _hidden = [];
    private int _nextInsertion;

    public void Seed(Lead entity)
    {
        _store[entity.Id] = entity;
        _insertionOrder.TryAdd(entity.Id, _nextInsertion++);
    }

    /// <summary>Puts an existing row outside the caller's hierarchy scope.</summary>
    public void HideFromCaller(Guid id) => _hidden.Add(id);

    /// <summary>Reads a row regardless of scope — for assertions only, never for the service.</summary>
    public Lead? Inspect(Guid id) => _store.GetValueOrDefault(id);

    public int Count => _store.Count;

    public Task<Lead?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_hidden.Contains(id) ? null : _store.GetValueOrDefault(id));

    public Task<IReadOnlyList<Lead>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Lead>>(Visible().ToList());

    public Task<IReadOnlyList<Lead>> ListOpenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Lead>>(Visible().Where(x => !x.ConvertedFlag).ToList());

    public Task<Lead?> FindOpenByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Visible().FirstOrDefault(x => x.CustomerId == customerId && !x.ConvertedFlag));

    public void Add(Lead entity) => Seed(entity);

    public void Update(Lead entity) => _store[entity.Id] = entity;

    private IEnumerable<Lead> Visible() =>
        _store.Values
            .Where(x => !_hidden.Contains(x.Id))
            .OrderByDescending(x => _insertionOrder[x.Id]);
}
