namespace Gearify.SharedKernel.AI;

public interface IAIService
{
    string ServiceName { get; }
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
