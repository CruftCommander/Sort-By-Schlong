using SortBySchlong.Core.Interfaces;
using SortBySchlong.Core.Models;

namespace SortBySchlong.Core.Tests.Integration;

/// <summary>
/// Fake implementation of IDesktopIconProvider for integration testing.
/// </summary>
public class FakeDesktopIconProvider : IDesktopIconProvider
{
    private readonly List<DesktopIcon> _icons;
    private readonly DesktopBounds _bounds;

    public FakeDesktopIconProvider(List<DesktopIcon> icons, DesktopBounds bounds)
    {
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));
        _bounds = bounds;
    }

    public Task<IReadOnlyList<DesktopIcon>> GetIconsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<DesktopIcon>>(_icons.ToList());
    }

    public Task<DesktopBounds> GetDesktopBoundsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_bounds);
    }
}

