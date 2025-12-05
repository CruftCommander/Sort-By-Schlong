#pragma once

#include <windows.h>

/// <summary>
/// Helper functions for building the SortBySchlong context menu.
/// Keeps QueryContextMenu implementation clean and maintainable.
/// </summary>
namespace MenuBuilder
{
    /// <summary>
    /// Adds the SortBySchlong submenu to the context menu.
    /// </summary>
    /// <param name="hmenu">Handle to the context menu</param>
    /// <param name="idFirst">First available command ID</param>
    /// <param name="idLast">Output parameter: last command ID used (idFirst + count - 1)</param>
    /// <returns>Number of command IDs consumed, or 0 on failure</returns>
    UINT AddSortBySchlongMenu(HMENU hmenu, UINT idFirst, UINT& idLast);
}

