# SortBySchlong – Desktop Icon Shape Arranger for Windows

SortBySchlong helps you arrange your Windows desktop icons by penis.

Unlike traditional UI-driven applications, this tool executes silently and is designed to be triggered by:
1. A simple **console harness** used for development and direct execution.
2. A planned **Windows Explorer context menu extension** that adds a new option: `Sort by → Penis`.

The first release includes one pre-defined shape provider:
- **PenisShapeProvider**

---

## Features
- Programmatically rearranges desktop icons using Win32 ListView messages
- Headless (heh) execution with no GUI components
- Clean modular architecture for additional shapes
- Designed for future support of a scriptable shape definition language
- Provides a console tool for easy debugging and testing
- Shell extension integration planned for Windows Explorer

---

## Solution Structure
```
/icon-arranger
  /src
    /IconArranger.Core              # Layout engine, Win32 interop, shape providers
    /IconArranger.ConsoleHarness    # CLI runner for execution and testing
    /IconArranger.Shell             # (Future) C++ COM shell extension
  /docs
    ARCHITECTURE.md
    TESTING.md
    SHELL_INTEGRATION.md
    SHAPE_LANGUAGE_FUTURE.md
```

---

## Technologies
- **C# .NET 8** for the core library and testing harness  
- **C++ Win32 COM** for the future Explorer context menu extension  
- **P/Invoke** for desktop ListView interaction  

The application targets Windows exclusively due to its reliance on Win32 APIs and Explorer internals.

---

## System Requirements & Compatibility

- **Operating System**: Windows 10 and Windows 11
- **Monitor Support**: Multiple monitor configurations are fully supported
- **.NET Runtime**: .NET 8.0 or later

The application automatically detects and works with the primary desktop ListView window, making it compatible with both single and multi-monitor setups. Icon arrangements are calculated based on the detected desktop bounds, ensuring proper positioning across all monitor configurations.

---

## Core Components

### Interfaces
- `IDesktopIconProvider` — retrieves icon metadata  
- `IIconLayoutApplier` — applies updated icon coordinates  
- `IShapeProvider` — generates layout points for each shape  
- `IShapeRegistry` — manages shape providers  
- `IShapeScriptEngine` — reserved for future script-generated shapes  

### Initial Implementations
- `DesktopIconService`
- `PenisShapeProvider`
- `ShapeRegistry`
- `NoopShapeScriptEngine`

---

## How the Engine Works
1. Enumerate desktop icons  
2. Determine desktop working bounds  
3. Generate the desired layout coordinates  
4. Apply the new icon positions  

The initial invocation pattern:
```
ArrangeDesktopIcons("penis")
```

---

## Console Harness Usage
The console harness is the primary entry point during early development.

### Default execution
```bash
dotnet run --project IconArranger.ConsoleHarness
```

### Specify shape explicitly
```bash
dotnet run --project IconArranger.ConsoleHarness -- --shape=penis
```

### List available shapes
```bash
dotnet run --project IconArranger.ConsoleHarness -- --list-shapes
```

### Help
```bash
dotnet run --project IconArranger.ConsoleHarness -- --help
```

**Important:** Running this tool will rearrange your actual desktop icons.

---

## Build Instructions

### Restore and build
```bash
dotnet restore
dotnet build -c Release
```

### Publish as a self-contained executable
```bash
dotnet publish IconArranger.ConsoleHarness/IconArranger.ConsoleHarness.csproj   -c Release   -r win-x64   --self-contained true   /p:PublishSingleFile=true   -o ./publish
```

### Run tests
```bash
dotnet test
```

---

## Shell Extension (Future Work)
The project intends to provide a shell extension that:
- Adds a new context menu entry under `Sort by`
- Launches the console harness upon selection

Development details will be documented in:
```
docs/SHELL_INTEGRATION.md
```

This integration will be added once the core engine is stable.

---

## Documentation
- **ARCHITECTURE.md** – Full system layout and design overview  
- **TESTING.md** – Guidance for manual and automated testing  
- **SHELL_INTEGRATION.md** – COM extension design notes  
- **SHAPE_LANGUAGE_FUTURE.md** – Concepts for script-based shape logic  

---

## Disclaimer
SortByShlong is a humorous but technically serious utility.  
Use responsibly — icon rearrangement affects your active desktop layout.

Enjoy, explore, and extend ;)
