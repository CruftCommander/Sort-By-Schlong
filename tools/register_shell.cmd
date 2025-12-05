@echo off
setlocal

set DLL_PATH=%~dp0..\x64\Release\SortBySchlong.Shell.dll

if not exist "%DLL_PATH%" (
    echo Error: DLL not found at %DLL_PATH%
    echo Please build the Release x64 configuration first.
    exit /b 1
)

echo Registering shell extension...
regsvr32 /s "%DLL_PATH%"

if %ERRORLEVEL% EQU 0 (
    echo Shell extension registered successfully.
    echo Please restart Explorer or log off/on for changes to take effect.
) else (
    echo Error: Registration failed with error code %ERRORLEVEL%
    exit /b 1
)

endlocal

