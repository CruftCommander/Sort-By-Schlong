using FluentAssertions;
using Moq;
using SortBySchlong.Core.Exceptions;
using SortBySchlong.Core.Interfaces;
using SortBySchlong.Core.Models;
using SortBySchlong.Core.Services;
using Xunit;
using Serilog;

namespace SortBySchlong.Core.Tests.Services;

public class IconArrangementServiceTests
{
    private readonly Mock<IDesktopIconProvider> _iconProviderMock;
    private readonly Mock<IIconLayoutApplier> _layoutApplierMock;
    private readonly Mock<IShapeRegistry> _shapeRegistryMock;
    private readonly ILogger _logger;
    private readonly IconArrangementService _service;

    public IconArrangementServiceTests()
    {
        _iconProviderMock = new Mock<IDesktopIconProvider>();
        _layoutApplierMock = new Mock<IIconLayoutApplier>();
        _shapeRegistryMock = new Mock<IShapeRegistry>();
        _logger = new LoggerConfiguration().CreateLogger();

        _service = new IconArrangementService(
            _iconProviderMock.Object,
            _layoutApplierMock.Object,
            _shapeRegistryMock.Object,
            _logger);
    }

    [Fact]
    public async Task ArrangeIconsAsync_WithValidShape_ShouldSucceed()
    {
        // Arrange
        var shapeKey = "penis";
        var icons = CreateIcons(10);
        var bounds = new DesktopBounds(1920, 1080);
        var layout = CreateLayout(10, bounds);

        var shapeProvider = new Mock<IShapeProvider>();
        shapeProvider.Setup(p => p.Key).Returns(shapeKey);
        shapeProvider.Setup(p => p.GenerateLayout(10, bounds)).Returns(layout);

        _shapeRegistryMock.Setup(r => r.GetProvider(shapeKey)).Returns(shapeProvider.Object);
        _iconProviderMock.Setup(p => p.GetIconsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(icons);
        _iconProviderMock.Setup(p => p.GetDesktopBoundsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(bounds);

        // Act
        await _service.ArrangeIconsAsync(shapeKey);

        // Assert
        _iconProviderMock.Verify(p => p.GetIconsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _iconProviderMock.Verify(p => p.GetDesktopBoundsAsync(It.IsAny<CancellationToken>()), Times.Once);
        shapeProvider.Verify(p => p.GenerateLayout(10, bounds), Times.Once);
        _layoutApplierMock.Verify(
            a => a.ApplyLayoutAsync(It.Is<IReadOnlyList<DesktopIcon>>(list => list.Count == 10), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ArrangeIconsAsync_WithNullShapeKey_ShouldThrowArgumentException()
    {
        Action act = () => _service.ArrangeIconsAsync(null!).GetAwaiter().GetResult();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ArrangeIconsAsync_WithEmptyShapeKey_ShouldThrowArgumentException()
    {
        Action act = () => _service.ArrangeIconsAsync("").GetAwaiter().GetResult();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ArrangeIconsAsync_WithWhitespaceShapeKey_ShouldThrowArgumentException()
    {
        Action act = () => _service.ArrangeIconsAsync("   ").GetAwaiter().GetResult();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ArrangeIconsAsync_WithNonExistentShape_ShouldThrowShapeNotFoundException()
    {
        var shapeKey = "nonexistent";
        _shapeRegistryMock.Setup(r => r.GetProvider(shapeKey)).Returns((IShapeProvider?)null);
        _shapeRegistryMock.Setup(r => r.GetAvailableShapes()).Returns(Array.Empty<string>());

        Func<Task> act = async () => await _service.ArrangeIconsAsync(shapeKey);

        await act.Should().ThrowAsync<ShapeNotFoundException>()
            .Where(ex => ex.ShapeKey == shapeKey);
    }

    [Fact]
    public async Task ArrangeIconsAsync_WithEmptyDesktop_ShouldReturnEarly()
    {
        var shapeKey = "penis";
        var bounds = new DesktopBounds(1920, 1080);

        var shapeProvider = new Mock<IShapeProvider>();
        shapeProvider.Setup(p => p.Key).Returns(shapeKey);

        _shapeRegistryMock.Setup(r => r.GetProvider(shapeKey)).Returns(shapeProvider.Object);
        _iconProviderMock.Setup(p => p.GetIconsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DesktopIcon>());
        _iconProviderMock.Setup(p => p.GetDesktopBoundsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(bounds);

        await _service.ArrangeIconsAsync(shapeKey);

        _layoutApplierMock.Verify(
            a => a.ApplyLayoutAsync(It.IsAny<IReadOnlyList<DesktopIcon>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ArrangeIconsAsync_WithIconCountMismatch_ShouldThrowInvalidLayoutException()
    {
        var shapeKey = "penis";
        var icons = CreateIcons(10);
        var bounds = new DesktopBounds(1920, 1080);
        var layout = CreateLayout(5, bounds); // Mismatch: 10 icons but 5 layout points

        var shapeProvider = new Mock<IShapeProvider>();
        shapeProvider.Setup(p => p.Key).Returns(shapeKey);
        shapeProvider.Setup(p => p.GenerateLayout(10, bounds)).Returns(layout);

        _shapeRegistryMock.Setup(r => r.GetProvider(shapeKey)).Returns(shapeProvider.Object);
        _iconProviderMock.Setup(p => p.GetIconsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(icons);
        _iconProviderMock.Setup(p => p.GetDesktopBoundsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(bounds);

        Func<Task> act = async () => await _service.ArrangeIconsAsync(shapeKey);

        await act.Should().ThrowAsync<InvalidLayoutException>()
            .WithMessage("*Layout count mismatch*");
    }

    [Fact]
    public async Task ArrangeIconsAsync_WithLayoutOutsideBounds_ShouldThrowInvalidLayoutException()
    {
        var shapeKey = "penis";
        var icons = CreateIcons(3);
        var bounds = new DesktopBounds(1920, 1080);
        var invalidLayout = new[]
        {
            new Point(0, 0),
            new Point(100, 100),
            new Point(3000, 3000) // Outside bounds
        };

        var shapeProvider = new Mock<IShapeProvider>();
        shapeProvider.Setup(p => p.Key).Returns(shapeKey);
        shapeProvider.Setup(p => p.GenerateLayout(3, bounds)).Returns(invalidLayout);

        _shapeRegistryMock.Setup(r => r.GetProvider(shapeKey)).Returns(shapeProvider.Object);
        _iconProviderMock.Setup(p => p.GetIconsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(icons);
        _iconProviderMock.Setup(p => p.GetDesktopBoundsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(bounds);

        Func<Task> act = async () => await _service.ArrangeIconsAsync(shapeKey);

        await act.Should().ThrowAsync<InvalidLayoutException>()
            .WithMessage("*outside desktop bounds*");
    }

    [Fact]
    public async Task ArrangeIconsAsync_WithCancellation_ShouldPropagateCancellation()
    {
        var shapeKey = "penis";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _shapeRegistryMock.Setup(r => r.GetProvider(shapeKey))
            .Returns(new Mock<IShapeProvider>().Object);

        Func<Task> act = async () => await _service.ArrangeIconsAsync(shapeKey, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static IReadOnlyList<DesktopIcon> CreateIcons(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new DesktopIcon(i, new Point(i * 50, i * 50), $"Icon{i}"))
            .ToList();
    }

    private static IReadOnlyList<Point> CreateLayout(int count, DesktopBounds bounds)
    {
        return Enumerable.Range(0, count)
            .Select(i => new Point(
                bounds.Width / (count + 1) * (i + 1),
                bounds.Height / (count + 1) * (i + 1)))
            .ToList();
    }
}

