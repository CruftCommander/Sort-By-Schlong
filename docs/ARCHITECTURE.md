# Architecture Documentation

## Overview

The Desktop Icon Arranger is a Windows tool that rearranges desktop icons into custom shapes. The system is architected with separation of concerns, dependency injection, and extensibility in mind.

## System Architecture

### High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    ConsoleHarness                            │
│  (Entry Point, CLI Parsing, DI Container)                   │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              IconArrangementService                          │
│  (Orchestration: Coordinate icon retrieval, shape           │
│   generation, and layout application)                        │
└─────┬──────────────────┬──────────────────┬─────────────────┘
      │                  │                  │
      ▼                  ▼                  ▼
┌─────────────┐  ┌──────────────┐  ┌──────────────────┐
│  Desktop    │  │  Shape       │  │  Layout          │
│  Icon       │  │  Registry    │  │  Applier         │
│  Provider   │  │              │  │                  │
└─────┬───────┘  └──────┬───────┘  └────────┬─────────┘
      │                  │                   │
      │                  ▼                   │
      │         ┌─────────────────┐          │
      │         │ Shape Providers │          │
      │         │  - PenisShape   │          │
      │         │  - (Future)     │          │
      │         └─────────────────┘          │
      │                                      │
      ▼                                      ▼
┌─────────────────────────────────────────────────────────────┐
│              DesktopIconService                              │
│  (P/Invoke Windows API: FindWindow, ListView messages)      │
└─────────────────────────────────────────────────────────────┘
```

## Core Interfaces

### IDesktopIconProvider

Provides access to desktop icons and their properties.

- `GetIconsAsync()`: Retrieves all icons from the desktop
- `GetDesktopBoundsAsync()`: Gets the bounds of the desktop ListView window

**Implementation**: `DesktopIconService`

### IIconLayoutApplier

Applies a layout to desktop icons by repositioning them.

- `ApplyLayoutAsync()`: Applies new positions to desktop icons

**Implementation**: `DesktopIconService`

### IShapeProvider

Generates layouts for arranging desktop icons in a specific shape.

- `Key`: Unique identifier for the shape
- `GenerateLayout()`: Generates a list of points representing icon positions

**Implementations**: `PenisShapeProvider` (and future shape providers)

### IShapeRegistry

Thread-safe registry for shape providers.

- `Register()`: Registers a shape provider
- `GetProvider()`: Retrieves a shape provider by key (case-insensitive)
- `GetAvailableShapes()`: Lists all available shape keys

**Implementation**: `ShapeRegistry`

### IShapeScriptEngine

Engine for executing scriptable shape definitions (future feature).

**Implementation**: `NoopShapeScriptEngine` (stub implementation)

## Data Flow

### Icon Arrangement Flow

1. **Console Harness** receives shape key from command line
2. **IconArrangementService** orchestrates:
   - Retrieves shape provider from **ShapeRegistry**
   - Calls **IDesktopIconProvider** to get icons and bounds
   - Calls **IShapeProvider.GenerateLayout()** to generate layout
   - Validates layout (count, bounds)
   - Calls **IIconLayoutApplier** to apply layout
3. **DesktopIconService** uses P/Invoke to:
   - Find desktop ListView window (Progman → SHELLDLL_DefView → SysListView32)
   - Enumerate icons (LVM_GETITEMCOUNT, LVM_GETITEMPOSITION)
   - Apply positions (LVM_SETITEMPOSITION)
   - Invalidate desktop for redraw

## Extension Points

### Adding New Shapes

1. Implement `IShapeProvider` interface
2. Register provider with `ShapeRegistry`
3. Provider will be automatically available via CLI

Example:
```csharp
public class CircleShapeProvider : IShapeProvider
{
    public string Key => "circle";
    
    public IReadOnlyList<Point> GenerateLayout(int iconCount, DesktopBounds bounds)
    {
        // Generate circular layout
    }
}

// Register in DI container
services.AddSingleton<IShapeProvider, CircleShapeProvider>();
```

### Future: Scriptable Shapes

The `IShapeScriptEngine` interface provides a hook for future scriptable shape definitions. Currently implemented as a no-op stub.

## Error Handling

### Exception Hierarchy

- `IconArrangementException` (base)
  - `ShapeNotFoundException`: Shape key not found
  - `DesktopAccessException`: Cannot access desktop window
  - `InvalidLayoutException`: Layout validation failures

All exceptions are logged with Serilog before being rethrown or wrapped.

### Logging

- Uses Serilog for structured logging
- Correlation IDs track operations across async boundaries
- Log levels:
  - Debug: Detailed operation information
  - Information: Successful operations
  - Warning: Recoverable issues
  - Error: Failures with context

## Thread Safety

- `ShapeRegistry` is thread-safe (ConcurrentDictionary)
- `DesktopIconService` is stateless and thread-safe for concurrent operations
- Shape providers should be stateless and thread-safe

## P/Invoke Windows API Usage

The system uses Vanara.PInvoke libraries to interact with Windows:

- **Vanara.PInvoke.User32**: Window discovery (FindWindow, FindWindowEx)
- **Vanara.PInvoke.ComCtl32**: ListView control messages (LVM_*)

Desktop window hierarchy:
1. Find "Progman" window (desktop manager)
2. Find "SHELLDLL_DefView" child window
3. Find "SysListView32" child window (actual icon container)

ListView messages:
- `LVM_GETITEMCOUNT` (0x1004): Get icon count
- `LVM_GETITEMPOSITION` (0x1010): Get icon position
- `LVM_SETITEMPOSITION` (0x100F): Set icon position
