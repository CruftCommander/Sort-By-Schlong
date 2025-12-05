#include "MenuBuilder.h"
#include "MenuConstants.h"
#include <vector>

namespace MenuBuilder
{
    // Structure for shape menu items (for future extensibility)
    struct ShapeMenuItem
    {
        const wchar_t* text;
        SortBySchlongUI::SortBySchlongCommand command;
    };
    
    // Static array of available shapes
    // To add a new shape, just add an entry here and update CommandCount in MenuConstants.h
    static const ShapeMenuItem s_shapes[] =
    {
        { SortBySchlongUI::MenuPenisText, SortBySchlongUI::SortBySchlongCommand::PenisLayout },
        // Future shapes can be added here:
        // { L"Stealth Mode", SortBySchlongUI::SortBySchlongCommand::StealthMode },
        // { L"Custom Shape...", SortBySchlongUI::SortBySchlongCommand::CustomShape },
    };
    
    UINT AddSortBySchlongMenu(HMENU hmenu, UINT idFirst, UINT& idLast)
    {
        if (hmenu == nullptr || !IsMenu(hmenu))
        {
            return 0;
        }
        
        // Validate command ID range
        if (idFirst >= 0x8000)
        {
            return 0;
        }
        
        HMENU hSubMenu = nullptr;
        
        try
        {
            // Create submenu for SortBySchlong
            hSubMenu = CreatePopupMenu();
            if (hSubMenu == nullptr || !IsMenu(hSubMenu))
            {
                if (hSubMenu != nullptr)
                {
                    DestroyMenu(hSubMenu);
                }
                return 0;
            }
            
            // Add shape items to submenu
            UINT currentId = idFirst;
            const UINT shapeCount = static_cast<UINT>(_countof(s_shapes));
            const UINT maxShapes = (shapeCount < SortBySchlongUI::CommandCount) ? shapeCount : SortBySchlongUI::CommandCount;
            UINT itemsAdded = 0;
            
            for (UINT i = 0; i < maxShapes; ++i)
            {
                const ShapeMenuItem& shape = s_shapes[i];
                
                // Validate text
                if (shape.text == nullptr)
                {
                    continue;
                }
                
                size_t textLen = wcslen(shape.text);
                if (textLen == 0 || textLen > 256)
                {
                    // Invalid text - skip this item
                    continue;
                }
                
                // Use AppendMenu which is simpler and more reliable
                if (!AppendMenuW(hSubMenu, MF_STRING, currentId, shape.text))
                {
                    // Cleanup on failure
                    DestroyMenu(hSubMenu);
                    return 0;
                }
                
                ++currentId;
                ++itemsAdded;
            }
            
            if (itemsAdded == 0)
            {
                // No items added - cleanup and return
                DestroyMenu(hSubMenu);
                return 0;
            }
            
            // Validate submenu still has items
            if (GetMenuItemCount(hSubMenu) != static_cast<int>(itemsAdded))
            {
                DestroyMenu(hSubMenu);
                return 0;
            }
            
            // Validate root menu text
            if (SortBySchlongUI::MenuRootText == nullptr)
            {
                DestroyMenu(hSubMenu);
                return 0;
            }
            
            size_t rootTextLen = wcslen(SortBySchlongUI::MenuRootText);
            if (rootTextLen == 0 || rootTextLen > 256)
            {
                DestroyMenu(hSubMenu);
                return 0;
            }
            
            // Add the submenu to the main context menu using AppendMenu
            // Note: After successful append, Windows takes ownership of hSubMenu
            if (!AppendMenuW(hmenu, MF_STRING | MF_POPUP, reinterpret_cast<UINT_PTR>(hSubMenu), SortBySchlongUI::MenuRootText))
            {
                DestroyMenu(hSubMenu);
                return 0;
            }
            
            // Windows now owns hSubMenu - don't destroy it
            hSubMenu = nullptr;
            
            // Calculate last ID used
            idLast = currentId - 1;
            
            // Return number of command IDs consumed
            return itemsAdded;
        }
        catch (...)
        {
            if (hSubMenu != nullptr)
            {
                DestroyMenu(hSubMenu);
            }
            return 0;
        }
    }
}

