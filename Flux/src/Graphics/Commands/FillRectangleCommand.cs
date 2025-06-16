using Vortice;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Flux.Graphics.Commands;

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