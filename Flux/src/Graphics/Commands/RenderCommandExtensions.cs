using System.Collections.Generic;
using System.Numerics;
using Vortice;
using Vortice.Mathematics;

namespace Flux.Graphics.Commands;

/// <summary>
///     Provides extension methods for adding render commands to a list, simplifying the syntax.
/// </summary>
public static class RenderCommandExtensions
{
    /// <summary>
    ///     Adds a command to draw text.
    /// </summary>
    public static void AddDrawText(this List<IRenderCommand> commands, string text, Vector2 position, Color4 color, float fontSize = 12f, string fontFamily = "Arial")
    {
        commands.Add(new DrawTextCommand
        {
            Text = text,
            FontFamily = fontFamily,
            FontSize = fontSize,
            Position = position,
            Color = color
        });
    }

    /// <summary>
    ///     Adds a command to draw a rectangle outline.
    /// </summary>
    public static void AddDrawRectangle(this List<IRenderCommand> commands, RawRectF rectangle, Color4 color, float strokeWidth = 1.0f)
    {
        commands.Add(new DrawRectangleCommand
        {
            Rectangle = rectangle,
            Color = color,
            StrokeWidth = strokeWidth
        });
    }

    /// <summary>
    ///     Adds a command to fill a rectangle.
    /// </summary>
    public static void AddFillRectangle(this List<IRenderCommand> commands, RawRectF rectangle, Color4 color)
    {
        commands.Add(new FillRectangleCommand
        {
            Rectangle = rectangle,
            Color = color
        });
    }
}