# Testing Documentation

## Overview

This document describes testing procedures, guidelines, and strategies for the Desktop Icon Arranger project.

## Test Structure

### Unit Tests

Located in `SortBySchlong.Core.Tests/`:

- **Shapes/**: Tests for shape providers (e.g., `PenisShapeProviderTests`)
- **Services/**: Tests for services (e.g., `ShapeRegistryTests`, `IconArrangementServiceTests`)

### Integration Tests

Located in `SortBySchlong.Core.Tests/Integration/`:

- Tests end-to-end flows using fake implementations
- `FakeDesktopIconProvider`: Simulates desktop without Windows API calls
- `FakeIconLayoutApplier`: Captures applied layouts for verification

## Running Tests

### Visual Studio

1. Open Test Explorer (Test → Test Explorer)
2. Build solution
3. Run all tests or select specific tests

### Command Line

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific test project
dotnet test SortBySchlong.Core.Tests/SortBySchlong.Core.Tests.csproj
```

### Test Output

Tests use xUnit framework with FluentAssertions for readable assertions.

## Test Coverage Goals

- **Target**: 80%+ coverage on Core library
- **Focus Areas**:
  - Business logic (shape generation, orchestration)
  - Error handling paths
  - Edge cases (empty desktop, invalid layouts, etc.)

## Unit Test Guidelines

### Structure (AAA Pattern)

```csharp
[Fact]
public void TestMethod_Scenario_ExpectedBehavior()
{
    // Arrange
    var service = new Service();
    
    // Act
    var result = service.DoSomething();
    
    // Assert
    result.Should().Be(expected);
}
```

### Mocking

- Use Moq for mocking interfaces
- Mock external dependencies (Windows APIs, file system, etc.)
- Verify interactions when appropriate

### Assertions

- Use FluentAssertions for readable assertions
- Be specific about expected behavior
- Test both success and failure paths

## Manual Testing Procedures

### Testing on Real Desktop

**Warning**: Manual testing will rearrange your actual desktop icons!

1. **Prepare**:
   - Backup your desktop icon positions (screenshot or manual note)
   - Ensure you have at least 3 icons on desktop

2. **Build**:
   ```bash
   dotnet build -c Release
   ```

3. **Run**:
   ```bash
   cd SortByShlong.ConsoleHarness/bin/Release/net8.0-windows
   .\SortBySchlong.ConsoleHarness.exe --shape=penis
   ```

4. **Verify**:
   - Check that icons are arranged in the expected shape
   - Verify no icons are positioned outside visible area
   - Check console output for errors

5. **Cleanup**:
   - Manually rearrange icons back to original positions
   - Or create a backup/restore mechanism

### Testing Different Icon Counts

1. Add or remove desktop icons to test various counts
2. Minimum: 3 icons (required for penis shape)
3. Test with 10, 20, 50+ icons to verify scaling

### Testing Different Screen Resolutions

1. Change display resolution
2. Run arrangement
3. Verify layout adapts correctly to new bounds

## Integration Test Scenarios

### Basic Arrangement

- Verify icons are arranged correctly
- Verify all positions are within bounds
- Verify icon count matches layout count

### Edge Cases

- Empty desktop (should return early)
- Icon count mismatch (should throw exception)
- Layout outside bounds (should throw exception)
- Invalid shape key (should throw exception)

### Cancellation

- Test cancellation token propagation
- Verify operations can be cancelled cleanly

## Continuous Integration

### Recommended CI Setup

```yaml
# Example GitHub Actions workflow
- name: Run Tests
  run: dotnet test --verbosity normal --collect:"XPlat Code Coverage"

- name: Generate Coverage Report
  run: reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage
```

## Test Data

### Fake Implementations

- **FakeDesktopIconProvider**: Pre-configured icons and bounds
- **FakeIconLayoutApplier**: Captures applied layouts for verification

These allow testing without Windows API dependencies.

## Troubleshooting

### Tests Failing Locally

1. Ensure .NET 8 SDK is installed
2. Restore packages: `dotnet restore`
3. Rebuild solution: `dotnet build`
4. Check test output for detailed error messages

### Coverage Not Generating

1. Install coverlet: `dotnet tool install -g coverlet.console`
2. Use correct format flags
3. Check output paths

## Future Test Enhancements

- Performance tests for large icon counts
- Stress tests for rapid arrangement changes
- UI automation tests (if GUI is added)
- Cross-version Windows compatibility tests
