namespace DotGlasses.Application.Common;

/// <summary>
/// Lets a service batch multiple repository writes (e.g. create a Lead and update its source
/// Test) into a single transaction. DotGlassesDbContext satisfies this shape directly — its own
/// DbContext.SaveChangesAsync(CancellationToken) already matches, no extra implementation needed
/// there. Test/Lead/Sale repositories track changes via Add/Update without saving; the calling
/// service calls this once per use case. AuditSaveChangesInterceptor still runs exactly once per
/// call regardless of how many entities changed, so this doesn't affect auditing.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
