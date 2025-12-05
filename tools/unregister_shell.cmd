@echo off
setlocal

set DLL_PATH=%~dp0..\x64\Release\SortBySchlong.Shell.dll

if not exist "%DLL_PATH%" (
    echo Error: DLL not found at %DLL_PATH%
    exit /b 1
)

echo Unregistering shell extension...
regsvr32 /u /s "%DLL_PATH%"

if %ERRORLEVEL% EQU 0 (
    echo Shell extension unregistered successfully.
    echo Please restart Explorer or log off/on for changes to take effect.
) else (
    echo Error: Unregistration failed with error code %ERRORLEVEL%
    exit /b 1
)

endlocal

