using SortBySchlong.Core.Interfaces;
using SortBySchlong.Core.Models;

namespace SortBySchlong.Core.Tests.Integration;

/// <summary>
/// Fake implementation of IIconLayoutApplier for integration testing.
/// </summary>
public class FakeIconLayoutApplier : IIconLayoutApplier
{
    public List<DesktopIcon> AppliedIcons { get; } = new();
    public bool WasCalled { get; private set; }

    public Task ApplyLayoutAsync(IReadOnlyList<DesktopIcon> icons, CancellationToken ct = default)
    {
        if (icons == null)
        {
            throw new ArgumentNullException(nameof(icons));
        }

        WasCalled = true;
        AppliedIcons.Clear();
        AppliedIcons.AddRange(icons);

        return Task.CompletedTask;
    }
}

