#include "ProcessLauncher.h"
#include <shlwapi.h>
#include <string>
#include <vector>

#pragma comment(lib, "shlwapi.lib")

namespace
{
    constexpr const wchar_t* CONSOLE_HARNESS_EXE = L"SortBySchlong.ConsoleHarness.exe";
}

bool ProcessLauncher::LaunchConsoleHarness(const std::wstring& shapeKey)
{
    try
    {
        // Get DLL directory
        std::wstring dllDir = GetDllDirectory();
        if (dllDir.empty())
        {
            LogDebug(L"ProcessLauncher: Failed to get DLL directory");
            return false;
        }

        // Build executable path
        std::wstring exePath = BuildConsoleHarnessPath(dllDir);

        // Build command line: "path\to\exe.exe" --shape=penis
        // CreateProcessW requires a writable buffer, so we use a vector
        std::wstring cmdLineStr = L"\"";
        cmdLineStr += exePath;
        cmdLineStr += L"\" --shape=";
        cmdLineStr += shapeKey;
        
        // Create writable buffer for command line
        std::vector<wchar_t> cmdLineBuffer(cmdLineStr.begin(), cmdLineStr.end());
        cmdLineBuffer.push_back(L'\0'); // Null terminator

        // Prepare process creation structures
        STARTUPINFOW si = {};
        si.cb = sizeof(si);
        si.dwFlags = STARTF_USESHOWWINDOW;
        si.wShowWindow = SW_HIDE; // Hide window

        PROCESS_INFORMATION pi = {};

        // Create process without window
        BOOL success = CreateProcessW(
            exePath.c_str(),           // Application name
            cmdLineBuffer.data(),      // Command line (writable buffer)
            NULL,                      // Process security attributes
            NULL,                      // Thread security attributes
            FALSE,                     // Inherit handles
            CREATE_NO_WINDOW,          // Creation flags
            NULL,                      // Environment
            NULL,                      // Current directory
            &si,                       // Startup info
            &pi                        // Process information
        );

        if (!success)
        {
            DWORD error = GetLastError();
            wchar_t errorMsg[256];
            swprintf_s(errorMsg, L"ProcessLauncher: CreateProcessW failed with error %lu", error);
            LogDebug(errorMsg);
            return false;
        }

        // Close handles immediately - we don't wait for the process
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);

        return true;
    }
    catch (...)
    {
        LogDebug(L"ProcessLauncher: Exception caught in LaunchConsoleHarness");
        return false;
    }
}

std::wstring ProcessLauncher::GetDllDirectory(HMODULE moduleHandle)
{
    try
    {
        wchar_t modulePath[MAX_PATH] = {};
        DWORD pathLen = GetModuleFileNameW(moduleHandle, modulePath, MAX_PATH);

        if (pathLen == 0 || pathLen >= MAX_PATH)
        {
            return std::wstring();
        }

        // Remove filename, keep only directory
        wchar_t* lastSlash = wcsrchr(modulePath, L'\\');
        if (lastSlash != nullptr)
        {
            *lastSlash = L'\0';
        }

        return std::wstring(modulePath);
    }
    catch (...)
    {
        return std::wstring();
    }
}

std::wstring ProcessLauncher::BuildConsoleHarnessPath(const std::wstring& dllDirectory)
{
    std::wstring path = dllDirectory;
    
    // Ensure path ends with backslash
    if (!path.empty() && path.back() != L'\\')
    {
        path += L'\\';
    }
    
    path += CONSOLE_HARNESS_EXE;
    return path;
}

void ProcessLauncher::LogDebug(const std::wstring& message)
{
    std::wstring fullMessage = L"[SortBySchlong.Shell] ";
    fullMessage += message;
    fullMessage += L"\r\n";
    OutputDebugStringW(fullMessage.c_str());
}

