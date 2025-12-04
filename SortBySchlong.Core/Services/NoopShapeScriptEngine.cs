using SortBySchlong.Core.Interfaces;
using SortBySchlong.Core.Models;

namespace SortBySchlong.Core.Services;

/// <summary>
/// No-op implementation of IShapeScriptEngine for future scriptable shape language support.
/// </summary>
public class NoopShapeScriptEngine : IShapeScriptEngine
{
    /// <inheritdoc/>
    public bool CanExecute(string script)
    {
        return false;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Point> Execute(string script, int iconCount, DesktopBounds bounds)
    {
        throw new NotSupportedException(
            "Scripted shapes are not yet supported. This feature is planned for a future release. " +
            "See docs/SHAPE_LANGUAGE_FUTURE.md for more information.");
    }
}

