#pragma once

#include <windows.h>
#include <string>

/// <summary>
/// Helper class for launching processes. Isolated from COM to allow unit testing.
/// </summary>
class ProcessLauncher
{
public:
    /// <summary>
    /// Launches the ConsoleHarness.exe from the same directory as the DLL.
    /// </summary>
    /// <param name="shapeKey">The shape key to pass as --shape parameter</param>
    /// <returns>true if successful, false otherwise. Errors are logged via OutputDebugString.</returns>
    static bool LaunchConsoleHarness(const std::wstring& shapeKey);

private:
    /// <summary>
    /// Gets the directory containing the DLL module.
    /// </summary>
    /// <param name="moduleHandle">Handle to the DLL module (or NULL for current module)</param>
    /// <returns>Directory path, or empty string on failure</returns>
    static std::wstring GetDllDirectory(HMODULE moduleHandle = NULL);

    /// <summary>
    /// Builds the full path to ConsoleHarness.exe in the DLL directory.
    /// </summary>
    /// <param name="dllDirectory">Directory containing the DLL</param>
    /// <returns>Full path to ConsoleHarness.exe</returns>
    static std::wstring BuildConsoleHarnessPath(const std::wstring& dllDirectory);

    /// <summary>
    /// Logs a debug message via OutputDebugStringW.
    /// </summary>
    static void LogDebug(const std::wstring& message);
};

