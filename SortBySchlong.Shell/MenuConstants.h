#pragma once

#include <windows.h>

namespace SortBySchlongUI
{
    // Menu text constants
    inline constexpr const wchar_t MenuRootText[] = L"SortBySchlong";
    inline constexpr const wchar_t MenuPenisText[] = L"Penis";
    
    // Optional verb string (for future use with string-based commands)
    inline constexpr const wchar_t MenuVerb[] = L"sortbyschlong";
    
    // Command ID enumeration
    // These represent relative offsets from idCmdFirst
    enum class SortBySchlongCommand : UINT
    {
        PenisLayout = 0,
        // Future commands can be added here:
        // StealthMode = 1,
        // CustomShape = 2,
        // Settings = 3,
    };
    
    // Total number of commands (update when adding new commands)
    inline constexpr UINT CommandCount = 1;
}

