using BlueTube.Core.Services;

namespace BlueTube.Tests.Fakes;

public sealed class FakeSecretStore : ISecretStore
{
    public Dictionary<string, string> Values { get; } = [];

    public Task<string?> GetAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(Values.TryGetValue(name, out var v) ? v : null);

    public Task SetAsync(string name, string? value, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(value)) Values.Remove(name);
        else Values[name] = value;
        return Task.CompletedTask;
    }
}
