@echo off
setlocal enabledelayedexpansion

REM Change to solution root directory (parent of tools)
pushd "%~dp0.."

REM Try Release first
if exist "x64\Release\SortBySchlong.Shell.dll" (
    set "DLL_PATH=%CD%\x64\Release\SortBySchlong.Shell.dll"
    echo Using Release build...
) else if exist "x64\Debug\SortBySchlong.Shell.dll" (
    set "DLL_PATH=%CD%\x64\Debug\SortBySchlong.Shell.dll"
    echo Warning: Using Debug build. For production, use Release build.
) else (
    echo Error: DLL not found in Release or Debug configuration.
    echo Expected locations:
    echo   %CD%\x64\Release\SortBySchlong.Shell.dll
    echo   %CD%\x64\Debug\SortBySchlong.Shell.dll
    echo.
    echo Please build the Release x64 or Debug x64 configuration first.
    popd
    exit /b 1
)

echo Registering shell extension from: %DLL_PATH%
echo.

regsvr32 /s "%DLL_PATH%"

set REG_RESULT=%ERRORLEVEL%
popd

if %REG_RESULT% EQU 0 (
    echo.
    echo Shell extension registered successfully.
    echo Please restart Explorer or log off/on for changes to take effect.
) else (
    echo.
    echo Error: Registration failed with error code %REG_RESULT%
    exit /b 1
)

endlocal
