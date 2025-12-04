using SortBySchlong.Core.Models;

namespace SortBySchlong.Core.Interfaces;

/// <summary>
/// Engine for executing scriptable shape definitions (future feature).
/// </summary>
public interface IShapeScriptEngine
{
    /// <summary>
    /// Determines whether this engine can execute the given script.
    /// </summary>
    /// <param name="script">The script to check.</param>
    /// <returns>True if the engine can execute the script; otherwise, false.</returns>
    bool CanExecute(string script);

    /// <summary>
    /// Executes a script to generate a layout.
    /// </summary>
    /// <param name="script">The script to execute.</param>
    /// <param name="iconCount">The number of icons to arrange.</param>
    /// <param name="bounds">The desktop bounds to arrange icons within.</param>
    /// <returns>A list of points representing icon positions.</returns>
    /// <exception cref="NotSupportedException">Thrown when script execution is not supported.</exception>
    IReadOnlyList<Point> Execute(string script, int iconCount, DesktopBounds bounds);
}

