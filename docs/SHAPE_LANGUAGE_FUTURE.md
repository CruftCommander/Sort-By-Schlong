# Future Shape Language Documentation

## Overview

This document describes the planned scriptable shape language that will allow users to define custom icon arrangements without writing C# code.

## Goals

- **Accessibility**: Non-programmers can create shapes
- **Expressiveness**: Support complex geometric patterns
- **Reusability**: Share shape definitions
- **Extensibility**: Support custom functions and primitives

## Proposed Syntax

### Basic Example

```yaml
shape:
  name: "circle"
  minIcons: 3
  
  layout:
    type: circle
    center: [50%, 50%]
    radius: 40%
    startAngle: 0
    endAngle: 360
```

### Shape Definition Structure

```yaml
shape:
  name: string              # Unique identifier
  displayName: string       # Human-readable name
  minIcons: number          # Minimum required icons
  maxIcons: number?         # Maximum supported icons (optional)
  
  parameters:               # Optional parameters
    - name: radius
      type: number
      default: 40
      min: 10
      max: 90
      unit: percent
  
  layout:                   # Layout definition
    type: <layout-type>
    # ... type-specific properties
```

## Layout Types

### Circle

```yaml
layout:
  type: circle
  center: [x, y]            # Center point (pixels or percent)
  radius: number            # Radius (pixels or percent)
  startAngle: number        # Start angle in degrees
  endAngle: number          # End angle in degrees
```

### Line

```yaml
layout:
  type: line
  start: [x1, y1]           # Start point
  end: [x2, y2]             # End point
  distribution: uniform     # uniform | exponential | custom
```

### Grid

```yaml
layout:
  type: grid
  rows: number
  cols: number
  spacing: [x, y]           # Spacing between items
  start: [x, y]             # Top-left corner
```

### Path

```yaml
layout:
  type: path
  points:                   # Control points
    - [x1, y1]
    - [x2, y2]
    - [x3, y3]
  interpolation: bezier     # linear | bezier | spline
```

### Composite

```yaml
layout:
  type: composite
  components:
    - type: circle
      center: [25%, 50%]
      radius: 20%
      iconCount: iconCount * 0.3
    - type: line
      start: [25%, 50%]
      end: [75%, 50%]
      iconCount: iconCount * 0.5
    - type: circle
      center: [75%, 50%]
      radius: 20%
      iconCount: iconCount * 0.2
```

## Coordinate Systems

### Absolute (Pixels)

```yaml
point: [1920, 1080]         # Absolute pixel coordinates
```

### Percentage

```yaml
point: [50%, 50%]           # Relative to bounds (width, height)
```

### Mixed

```yaml
point: [50%, 1080]          # X as percentage, Y as pixels
```

## Expressions

### Variables

- `iconCount`: Number of icons to arrange
- `bounds.width`: Desktop width
- `bounds.height`: Desktop height
- `bounds.center`: Center point [x, y]

### Math Operations

```yaml
radius: iconCount * 10
spacing: bounds.width / iconCount
angle: 360 / iconCount
```

### Functions

```yaml
radius: min(bounds.width, bounds.height) * 0.4
angle: sin(iconCount) * 360
center: [bounds.width / 2, bounds.height / 2]
```

## IShapeScriptEngine Interface Extension

### Proposed Extension

```csharp
public interface IShapeScriptEngine
{
    // Existing methods
    bool CanExecute(string script);
    IReadOnlyList<Point> Execute(string script, int iconCount, DesktopBounds bounds);
    
    // New methods for scriptable shapes
    IShapeProvider? LoadShapeFromScript(string script);
    IReadOnlyList<string> GetSupportedFormats();
    ValidationResult ValidateScript(string script);
}
```

### Implementation Considerations

1. **Parser**: YAML parser (YamlDotNet) or JSON
2. **Expression Evaluator**: Math expression evaluator
3. **Validation**: Schema validation for shape definitions
4. **Caching**: Cache parsed/compiled shapes for performance

## File Format

### YAML Format (Preferred)

```yaml
# shape.yaml
shape:
  name: custom-circle
  displayName: "Custom Circle"
  minIcons: 5
  
  layout:
    type: circle
    center: [bounds.width / 2, bounds.height / 2]
    radius: min(bounds.width, bounds.height) * 0.3
```

### JSON Format (Alternative)

```json
{
  "shape": {
    "name": "custom-circle",
    "displayName": "Custom Circle",
    "minIcons": 5,
    "layout": {
      "type": "circle",
      "center": ["bounds.width / 2", "bounds.height / 2"],
      "radius": "min(bounds.width, bounds.height) * 0.3"
    }
  }
}
```

## Example Shapes

### Spiral

```yaml
shape:
  name: spiral
  minIcons: 10
  
  layout:
    type: spiral
    center: [50%, 50%]
    turns: 3
    radiusStart: 5%
    radiusEnd: 45%
```

### Heart

```yaml
shape:
  name: heart
  minIcons: 20
  
  layout:
    type: composite
    components:
      - type: path
        points:
          - [50%, 30%]
          - [35%, 20%]
          - [25%, 35%]
          - [25%, 50%]
        iconCount: iconCount * 0.5
      - type: path
        points:
          - [50%, 30%]
          - [65%, 20%]
          - [75%, 35%]
          - [75%, 50%]
        iconCount: iconCount * 0.5
```

### Text/Word

```yaml
shape:
  name: hello
  minIcons: 50
  
  layout:
    type: text
    text: "HELLO"
    font: monospace
    size: 200
    position: [50%, 50%]
    anchor: center
```

## Integration Plan

### Phase 1: Basic Parser

- YAML/JSON parser
- Simple layout types (circle, line, grid)
- Basic expression evaluation

### Phase 2: Advanced Features

- Composite layouts
- Path interpolation
- Custom functions

### Phase 3: User Interface

- Shape editor (optional GUI)
- Shape gallery/library
- Import/export functionality

## Implementation Notes

- Consider using a scripting engine (e.g., Lua, JavaScript) for expressions
- Cache compiled/parsed shapes for performance
- Validate shapes before execution to catch errors early
- Provide helpful error messages with line numbers for invalid scripts
