using FluentAssertions;
using SortBySchlong.Core.Interfaces;
using SortBySchlong.Core.Models;
using SortBySchlong.Core.Services;
using SortBySchlong.Core.Shapes;
using Xunit;
using Serilog;

namespace SortBySchlong.Core.Tests.Integration;

public class IconArrangementServiceIntegrationTests
{
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    [Fact]
    public async Task ArrangeIconsAsync_WithFakeDesktop_ShouldCompleteSuccessfully()
    {
        // Arrange
        var icons = new List<DesktopIcon>
        {
            new(0, new Point(100, 100), "Icon1"),
            new(1, new Point(200, 200), "Icon2"),
            new(2, new Point(300, 300), "Icon3"),
        };

        var bounds = new DesktopBounds(1920, 1080);
        var fakeIconProvider = new FakeDesktopIconProvider(icons, bounds);
        var fakeLayoutApplier = new FakeIconLayoutApplier();
        var shapeRegistry = new ShapeRegistry();
        var penisProvider = new PenisShapeProvider();
        shapeRegistry.Register(penisProvider);

        var service = new IconArrangementService(
            fakeIconProvider,
            fakeLayoutApplier,
            shapeRegistry,
            _logger);

        // Act
        await service.ArrangeIconsAsync("penis");

        // Assert
        fakeLayoutApplier.WasCalled.Should().BeTrue();
        fakeLayoutApplier.AppliedIcons.Should().HaveCount(3);
        fakeLayoutApplier.AppliedIcons.Should().OnlyContain(icon => bounds.Contains(icon.Position));
    }

    [Fact]
    public async Task ArrangeIconsAsync_WithVariousIconCounts_ShouldWorkCorrectly()
    {
        var bounds = new DesktopBounds(1920, 1080);

        for (int iconCount = 3; iconCount <= 20; iconCount++)
        {
            // Arrange
            var icons = Enumerable.Range(0, iconCount)
                .Select(i => new DesktopIcon(i, new Point(i * 50, i * 50), $"Icon{i}"))
                .ToList();

            var fakeIconProvider = new FakeDesktopIconProvider(icons, bounds);
            var fakeLayoutApplier = new FakeIconLayoutApplier();
            var shapeRegistry = new ShapeRegistry();
            var penisProvider = new PenisShapeProvider();
            shapeRegistry.Register(penisProvider);

            var service = new IconArrangementService(
                fakeIconProvider,
                fakeLayoutApplier,
                shapeRegistry,
                _logger);

            // Act
            await service.ArrangeIconsAsync("penis");

            // Assert
            fakeLayoutApplier.AppliedIcons.Should().HaveCount(iconCount);
            fakeLayoutApplier.AppliedIcons.Should().OnlyContain(icon => bounds.Contains(icon.Position));
        }
    }

    [Fact]
    public async Task ArrangeIconsAsync_WithCoordinateValidation_ShouldEnsureAllPointsInBounds()
    {
        // Arrange
        var icons = Enumerable.Range(0, 15)
            .Select(i => new DesktopIcon(i, new Point(i * 50, i * 50), $"Icon{i}"))
            .ToList();

        var bounds = new DesktopBounds(1920, 1080);
        var fakeIconProvider = new FakeDesktopIconProvider(icons, bounds);
        var fakeLayoutApplier = new FakeIconLayoutApplier();
        var shapeRegistry = new ShapeRegistry();
        var penisProvider = new PenisShapeProvider();
        shapeRegistry.Register(penisProvider);

        var service = new IconArrangementService(
            fakeIconProvider,
            fakeLayoutApplier,
            shapeRegistry,
            _logger);

        // Act
        await service.ArrangeIconsAsync("penis");

        // Assert - All applied positions should be within bounds
        foreach (var icon in fakeLayoutApplier.AppliedIcons)
        {
            bounds.Contains(icon.Position).Should().BeTrue(
                $"Icon at index {icon.Index} has position ({icon.Position.X}, {icon.Position.Y}) which is outside bounds");
        }
    }
}

