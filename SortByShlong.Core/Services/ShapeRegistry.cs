using System.Collections.Concurrent;
using SortBySchlong.Core.Interfaces;

namespace SortBySchlong.Core.Services;

/// <summary>
/// Thread-safe registry for shape providers.
/// </summary>
public class ShapeRegistry : IShapeRegistry
{
    private readonly ConcurrentDictionary<string, IShapeProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Register(IShapeProvider provider)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(provider.Key))
        {
            throw new ArgumentException("Shape provider key cannot be null or whitespace.", nameof(provider));
        }

        if (!_providers.TryAdd(provider.Key, provider))
        {
            throw new ArgumentException($"A shape provider with key '{provider.Key}' is already registered.", nameof(provider));
        }
    }

    /// <inheritdoc/>
    public IShapeProvider? GetProvider(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        _providers.TryGetValue(key, out var provider);
        return provider;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> GetAvailableShapes()
    {
        return _providers.Keys.ToList().AsReadOnly();
    }
}

