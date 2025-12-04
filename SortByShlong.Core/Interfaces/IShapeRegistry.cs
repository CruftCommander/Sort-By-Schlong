namespace SortBySchlong.Core.Interfaces;

/// <summary>
/// Registry for shape providers that can arrange desktop icons.
/// </summary>
public interface IShapeRegistry
{
    /// <summary>
    /// Registers a shape provider.
    /// </summary>
    /// <param name="provider">The shape provider to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when provider is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a provider with the same key is already registered.</exception>
    void Register(IShapeProvider provider);

    /// <summary>
    /// Gets a shape provider by its key.
    /// </summary>
    /// <param name="key">The key of the shape provider to retrieve.</param>
    /// <returns>The shape provider if found; otherwise, null.</returns>
    IShapeProvider? GetProvider(string key);

    /// <summary>
    /// Gets all available shape keys.
    /// </summary>
    /// <returns>A collection of available shape keys.</returns>
    IReadOnlyCollection<string> GetAvailableShapes();
}

