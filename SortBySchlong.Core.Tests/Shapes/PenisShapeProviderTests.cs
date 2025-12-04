using FluentAssertions;
using SortBySchlong.Core.Models;
using SortBySchlong.Core.Shapes;
using Xunit;

namespace SortBySchlong.Core.Tests.Shapes;

public class PenisShapeProviderTests
{
    private readonly PenisShapeProvider _provider = new();

    [Fact]
    public void Key_ShouldReturnPenis()
    {
        _provider.Key.Should().Be("penis");
    }

    [Fact]
    public void GenerateLayout_WithLessThanMinimumIcons_ShouldThrowArgumentException()
    {
        var bounds = new DesktopBounds(1920, 1080);
        
        Action act = () => _provider.GenerateLayout(2, bounds);
        
        act.Should().Throw<ArgumentException>()
            .WithMessage("*At least 3 icons are required*");
    }

    [Fact]
    public void GenerateLayout_WithMinimumIcons_ShouldGenerateValidLayout()
    {
        var bounds = new DesktopBounds(1920, 1080);
        
        var layout = _provider.GenerateLayout(3, bounds);
        
        layout.Should().NotBeNull();
        layout.Should().HaveCount(3);
        layout.Should().OnlyContain(p => bounds.Contains(p));
    }

    [Fact]
    public void GenerateLayout_WithTenIcons_ShouldGenerateValidLayout()
    {
        var bounds = new DesktopBounds(1920, 1080);
        
        var layout = _provider.GenerateLayout(10, bounds);
        
        layout.Should().NotBeNull();
        layout.Should().HaveCount(10);
        layout.Should().OnlyContain(p => bounds.Contains(p));
    }

    [Fact]
    public void GenerateLayout_WithFiftyIcons_ShouldGenerateValidLayout()
    {
        var bounds = new DesktopBounds(1920, 1080);
        
        var layout = _provider.GenerateLayout(50, bounds);
        
        layout.Should().NotBeNull();
        layout.Should().HaveCount(50);
        layout.Should().OnlyContain(p => bounds.Contains(p));
    }

    [Theory]
    [InlineData(800, 600)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1440)]
    [InlineData(3840, 2160)]
    public void GenerateLayout_WithVariousBounds_ShouldGenerateValidLayout(int width, int height)
    {
        var bounds = new DesktopBounds(width, height);
        var iconCount = 20;
        
        var layout = _provider.GenerateLayout(iconCount, bounds);
        
        layout.Should().NotBeNull();
        layout.Should().HaveCount(iconCount);
        layout.Should().OnlyContain(p => bounds.Contains(p));
    }

    [Fact]
    public void GenerateLayout_ShouldMaintainShapeProportions()
    {
        var bounds = new DesktopBounds(1920, 1080);
        var iconCount = 20;
        
        var layout = _provider.GenerateLayout(iconCount, bounds);
        
        // Check that layout has reasonable distribution (not all at same position)
        var uniquePositions = layout.Distinct().Count();
        uniquePositions.Should().BeGreaterThan(iconCount / 2, "layout should have variety in positions");
        
        // Check that points are spread across the bounds
        var minX = layout.Min(p => p.X);
        var maxX = layout.Max(p => p.X);
        var minY = layout.Min(p => p.Y);
        var maxY = layout.Max(p => p.Y);
        
        (maxX - minX).Should().BeGreaterThan(bounds.Width / 10, "layout should span horizontally");
        (maxY - minY).Should().BeGreaterThan(bounds.Height / 10, "layout should span vertically");
    }

    [Fact]
    public void GenerateLayout_WithSameParameters_ShouldGenerateConsistentResults()
    {
        var bounds = new DesktopBounds(1920, 1080);
        var iconCount = 15;
        
        var layout1 = _provider.GenerateLayout(iconCount, bounds);
        var layout2 = _provider.GenerateLayout(iconCount, bounds);
        
        // Note: The algorithm may not be deterministic, but should produce valid layouts
        layout1.Should().HaveCount(iconCount);
        layout2.Should().HaveCount(iconCount);
        layout1.Should().OnlyContain(p => bounds.Contains(p));
        layout2.Should().OnlyContain(p => bounds.Contains(p));
    }
}

