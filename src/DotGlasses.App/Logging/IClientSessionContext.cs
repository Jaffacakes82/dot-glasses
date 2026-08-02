namespace DotGlasses.App.Logging;

/// <summary>One correlation id per app session, reused across every client-log batch that
/// session ships — lets a support issue be traced across client and server logs.</summary>
public interface IClientSessionContext
{
    Guid SessionCorrelationId { get; }
}

public class ClientSessionContext : IClientSessionContext
{
    public Guid SessionCorrelationId { get; } = Guid.NewGuid();
}
