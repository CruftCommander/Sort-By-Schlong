# Project Plan - Windows Desktop Icon Shape Arranger (Headless, Context Menu Only)

## 1. Overview
This project is a headless Windows tool that:
- Rearranges desktop icons into a predefined **penis** shape.
- Adds a desktop right-click context menu entry: `Sort by -> Penis`
- Provides a console harness used for dev/testing.
- Is architected for future shapes and a scriptable shape language.

The initial version includes only:
- `PenisShapeProvider`

---

## 2. Architecture Overview

### 2.1 Solution Structure
/icon-arranger
  /src
    /IconArranger.Core
    /IconArranger.ConsoleHarness
    /IconArranger.Shell
  /docs
    ARCHITECTURE.md
    TESTING.md
    SHELL_INTEGRATION.md
    SHAPE_LANGUAGE_FUTURE.md

### 2.2 Technologies
- Core + Console → C# .NET 8
- Context Menu Shell Extension → C++ Win32 COM
- P/Invoke for Windows ListView manipulation

### 2.3 Core Interfaces & Types
- IDesktopIconProvider
- IIconLayoutApplier
- IShapeProvider
- IShapeRegistry
- IShapeScriptEngine (stub)

Concrete v1:
- DesktopIconService
- PenisShapeProvider
- ShapeRegistry
- NoopShapeScriptEngine

---

## 3. Repo & Solution Setup

### 3.1 Initial repo setup
- Create repo, branches, README, .editorconfig, .gitignore

### 3.2 Create projects
- Core (C#)
- ConsoleHarness (C#)
- Shell extension (C++)

---

## 4. Desktop Interaction Engine

### 4.1 Handle Discovery
- Find desktop ListView window using:
  - FindWindow("Progman")
  - FindWindowEx to locate SHELLDLL_DefView
  - FindWindowEx SysListView32

### 4.2 Icon Enumeration
- LVM_GETITEMCOUNT
- LVM_GETITEMPOSITION
- LVM_GETITEMTEXT (optional)

### 4.3 Layout Application
- LVM_SETITEMPOSITION
- InvalidateRect for redraw

### 4.4 Desktop Bounds
- Use ListView client rect

---

## 5. Shape Architecture

### 5.1 IShapeProvider
Each provider supplies:
- string Key
- GenerateLayout(int iconCount, DesktopBounds bounds)

### 5.2 PenisShapeProvider
Produces:
- Shaft (line)
- Head (curved segment)
- Balls (two ellipses)

### 5.3 ShapeRegistry
Registers:
- “penis” → PenisShapeProvider

Future:
- Additional shapes
- Scripted shapes

---

## 6. Orchestration Logic

ArrangeDesktopIcons("penis"):
1. Get icons
2. Get desktop bounds
3. Compute layout
4. Apply layout

---

## 7. Console Harness (Testing)

### 7.1 Behavior
Running the exe:
- Applies penis arrangement
- Exits with 0 on success

### 7.2 CLI (future)
--shape=penis

### 7.3 Dev testing
- Move icons manually
- Run console harness
- Validate layout

---

## 8. Shell Integration (Sort by → Penis)

### 8.1 Implementation
- C++ COM extension:
  - Implements IContextMenu, IShellExtInit
  - Adds menu item under “Sort by”
  - On click → launches console:
    IconArranger.ConsoleHarness.exe --shape=penis

### 8.2 Registration
regsvr32 IconArranger.Shell.dll

### 8.3 Debugging
- Attach to explorer.exe
- Validate proper menu injection

---

## 9. Testing & Quality

### 9.1 Unit Tests
- PenisShapeProvider layout generation
- Bounds scaling
- Mocked orchestration flow

### 9.2 Integration Tests
- Fake desktop service for coordinate validation

### 9.3 Manual Tests
- Document steps in docs/TESTING.md

---

## 10. Future Scriptable Shape Language

### 10.1 Interface stub
IShapeScriptEngine

### 10.2 Noop implementation
- Included in v1

### 10.3 Planning doc
- docs/SHAPE_LANGUAGE_FUTURE.md

---

## 11. Building & Running

### 11.1 Build Instructions

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build -c Release

# Build specific project
dotnet build SortByShlong.Core/SortBySchlong.Core.csproj -c Release
```

### 11.2 Running the Console Harness

```bash
# Run with default shape (penis)
dotnet run --project SortByShlong.ConsoleHarness

# Run with specific shape
dotnet run --project SortByShlong.ConsoleHarness -- --shape=penis

# List available shapes
dotnet run --project SortByShlong.ConsoleHarness -- --list-shapes

# Help
dotnet run --project SortByShlong.ConsoleHarness -- --help
```

### 11.3 Publishing for Deployment

```bash
# Publish console application (self-contained)
dotnet publish SortByShlong.ConsoleHarness/SortBySchlong.ConsoleHarness.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  /p:PublishSingleFile=true \
  -o ./publish
```

### 11.4 Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific test project
dotnet test SortBySchlong.Core.Tests/SortBySchlong.Core.Tests.csproj
```

### 11.5 Manual Testing

**Warning**: Running the application will rearrange your desktop icons!

1. Ensure you have at least 3 icons on your desktop
2. Build the solution in Release mode
3. Run the console harness:
   ```bash
   cd SortByShlong.ConsoleHarness/bin/Release/net8.0-windows
   .\SortBySchlong.ConsoleHarness.exe --shape=penis
   ```
4. Verify icons are arranged correctly
5. Manually restore icon positions if needed

See [docs/TESTING.md](docs/TESTING.md) for detailed testing procedures.

## 12. Documentation

- [Architecture Documentation](docs/ARCHITECTURE.md): System design and component overview
- [Testing Documentation](docs/TESTING.md): Testing procedures and guidelines
- [Shell Integration Documentation](docs/SHELL_INTEGRATION.md): C++ COM extension integration plan
- [Future Shape Language Documentation](docs/SHAPE_LANGUAGE_FUTURE.md): Planned scriptable shape language

## 13. Packaging & Deployment

### 13.1 Publishing console
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

### 13.2 Deploy shell extension
- Copy DLL + EXE
- Register DLL (see [Shell Integration Documentation](docs/SHELL_INTEGRATION.md))
