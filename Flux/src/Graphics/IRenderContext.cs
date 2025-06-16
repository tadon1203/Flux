using System.Numerics;
using Vortice;
using Vortice.Mathematics;

namespace Flux.Graphics;

/// <summary>
/// Defines an interface for issuing render commands.
/// This abstracts away the underlying command list, providing a clear API for drawing.
/// </summary>
public interface IRenderContext
{
    /// <summary>
    /// Issues a command to draw text.
    /// </summary>
    void DrawText(string text, Vector2 position, Color4 color, float fontSize = 12f, string fontFamily = "Arial");

    /// <summary>
    /// Issues a command to draw a rectangle outline.
    /// </summary>
    void DrawRectangle(RawRectF rectangle, Color4 color, float strokeWidth = 1.0f);

    /// <summary>
    /// Issues a command to fill a rectangle.
    /// </summary>
    void FillRectangle(RawRectF rectangle, Color4 color);
}