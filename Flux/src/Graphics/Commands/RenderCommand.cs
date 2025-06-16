using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace Flux.Graphics.Commands;

public interface IRenderCommand
{
    void Execute(D2DRenderer renderer);
}

public class DrawTextCommand : IRenderCommand
{
    public string Text { get; set; }
    public string FontFamily { get; set; }
    public float FontSize { get; set; }
    public Vector2 Position { get; set; }
    public Color4 Color { get; set; }

    public void Execute(D2DRenderer renderer)
    {
        IDWriteTextFormat textFormat = renderer.GetOrCreateTextFormat(FontFamily, FontSize);
        ID2D1SolidColorBrush brush = renderer.GetOrCreateBrush(Color);
        var layoutRect = new RawRectF(Position.X, Position.Y, float.MaxValue, float.MaxValue);
        renderer.Context.DrawText(Text, textFormat, layoutRect, brush);
    }
}

public class DrawRectangleCommand : IRenderCommand
{
    public RawRectF Rectangle { get; set; }
    public Color4 Color { get; set; }
    public float StrokeWidth { get; set; }

    public void Execute(D2DRenderer renderer)
    {
        ID2D1SolidColorBrush brush = renderer.GetOrCreateBrush(Color);
        renderer.Context.DrawRectangle(Rectangle, brush, StrokeWidth);
    }
}

public class FillRectangleCommand : IRenderCommand
{
    public RawRectF Rectangle { get; set; }
    public Color4 Color { get; set; }

    public void Execute(D2DRenderer renderer)
    {
        ID2D1SolidColorBrush brush = renderer.GetOrCreateBrush(Color);
        renderer.Context.FillRectangle(Rectangle, brush);
    }
}