namespace Gearify.SharedKernel.AI.Caching;

public interface IAICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default) where T : class;
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiry, CancellationToken cancellationToken = default) where T : class;
}
