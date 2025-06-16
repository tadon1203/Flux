using System.Numerics;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace Flux.Graphics.Commands;

public class DrawTextCommand : IRenderCommand
{
    public string Text { get; set; }
    public FontFamily Font { get; set; }
    public FontWeight Weight { get; set; }
    public float FontSize { get; set; }
    public Vector2 Position { get; set; }
    public Color4 Color { get; set; }

    public void Execute(D2DRenderer renderer)
    {
        IDWriteTextFormat textFormat = renderer.GetOrCreateTextFormat(Font, Weight, FontSize);
        ID2D1SolidColorBrush brush = renderer.GetOrCreateBrush(Color);
        var layoutRect = new RawRectF(Position.X, Position.Y, float.MaxValue, float.MaxValue);
        renderer.Context.DrawText(Text, textFormat, layoutRect, brush);
    }
}