using System.Collections.Generic;
using System.Numerics;
using Flux.Graphics.Commands;
using Vortice;
using Vortice.Mathematics;

namespace Flux.Graphics;

/// <summary>
///     An implementation of IRenderContext that builds a list of render commands.
/// </summary>
public class RenderContext : IRenderContext
{
    private readonly List<IRenderCommand> _commands = new();

    public IReadOnlyList<IRenderCommand> Commands => _commands;

    public void DrawText(string text, Vector2 position, Color4 color, float fontSize = 12f, FontFamily font = FontFamily.Inter, FontWeight weight = FontWeight.Regular)
    {
        _commands.Add(new DrawTextCommand
        {
            Text = text,
            Font = font,
            Weight = weight,
            FontSize = fontSize,
            Position = position,
            Color = color
        });
    }

    public void DrawRectangle(RawRectF rectangle, Color4 color, float strokeWidth = 1.0f)
    {
        _commands.Add(new DrawRectangleCommand
        {
            Rectangle = rectangle,
            Color = color,
            StrokeWidth = strokeWidth
        });
    }

    public void FillRectangle(RawRectF rectangle, Color4 color)
    {
        _commands.Add(new FillRectangleCommand
        {
            Rectangle = rectangle,
            Color = color
        });
    }
}