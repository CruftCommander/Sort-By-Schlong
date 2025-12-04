using FluentAssertions;
using Moq;
using SortBySchlong.Core.Interfaces;
using SortBySchlong.Core.Services;
using Xunit;

namespace SortBySchlong.Core.Tests.Services;

public class ShapeRegistryTests
{
    private readonly ShapeRegistry _registry = new();

    [Fact]
    public void Register_WithValidProvider_ShouldSucceed()
    {
        var provider = CreateMockProvider("test");

        _registry.Register(provider.Object);

        var retrieved = _registry.GetProvider("test");
        retrieved.Should().Be(provider.Object);
    }

    [Fact]
    public void Register_WithNullProvider_ShouldThrowArgumentNullException()
    {
        Action act = () => _registry.Register(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_WithDuplicateKey_ShouldThrowArgumentException()
    {
        var provider1 = CreateMockProvider("duplicate");
        var provider2 = CreateMockProvider("duplicate");

        _registry.Register(provider1.Object);

        Action act = () => _registry.Register(provider2.Object);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void Register_WithCaseInsensitiveKey_ShouldTreatAsDuplicate()
    {
        var provider1 = CreateMockProvider("Test");
        var provider2 = CreateMockProvider("TEST");

        _registry.Register(provider1.Object);

        Action act = () => _registry.Register(provider2.Object);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void GetProvider_WithExistingKey_ShouldReturnProvider()
    {
        var provider = CreateMockProvider("existing");
        _registry.Register(provider.Object);

        var result = _registry.GetProvider("existing");

        result.Should().Be(provider.Object);
    }

    [Fact]
    public void GetProvider_WithCaseInsensitiveKey_ShouldReturnProvider()
    {
        var provider = CreateMockProvider("TestShape");
        _registry.Register(provider.Object);

        var result1 = _registry.GetProvider("testshape");
        var result2 = _registry.GetProvider("TESTSHAPE");
        var result3 = _registry.GetProvider("TestShape");

        result1.Should().Be(provider.Object);
        result2.Should().Be(provider.Object);
        result3.Should().Be(provider.Object);
    }

    [Fact]
    public void GetProvider_WithNonExistentKey_ShouldReturnNull()
    {
        var result = _registry.GetProvider("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public void GetProvider_WithNullKey_ShouldReturnNull()
    {
        var result = _registry.GetProvider(null!);

        result.Should().BeNull();
    }

    [Fact]
    public void GetProvider_WithWhitespaceKey_ShouldReturnNull()
    {
        var result = _registry.GetProvider("   ");

        result.Should().BeNull();
    }

    [Fact]
    public void GetAvailableShapes_WithNoRegisteredShapes_ShouldReturnEmpty()
    {
        var shapes = _registry.GetAvailableShapes();

        shapes.Should().BeEmpty();
    }

    [Fact]
    public void GetAvailableShapes_WithRegisteredShapes_ShouldReturnAllKeys()
    {
        var provider1 = CreateMockProvider("shape1");
        var provider2 = CreateMockProvider("shape2");
        var provider3 = CreateMockProvider("shape3");

        _registry.Register(provider1.Object);
        _registry.Register(provider2.Object);
        _registry.Register(provider3.Object);

        var shapes = _registry.GetAvailableShapes();

        shapes.Should().HaveCount(3);
        shapes.Should().Contain("shape1");
        shapes.Should().Contain("shape2");
        shapes.Should().Contain("shape3");
    }

    [Fact]
    public void Register_WithEmptyKey_ShouldThrowArgumentException()
    {
        var provider = CreateMockProvider("");

        Action act = () => _registry.Register(provider.Object);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or whitespace*");
    }

    [Fact]
    public void Register_WithWhitespaceKey_ShouldThrowArgumentException()
    {
        var provider = CreateMockProvider("   ");

        Action act = () => _registry.Register(provider.Object);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or whitespace*");
    }

    private static Mock<IShapeProvider> CreateMockProvider(string key)
    {
        var mock = new Mock<IShapeProvider>();
        mock.Setup(p => p.Key).Returns(key);
        return mock;
    }
}

