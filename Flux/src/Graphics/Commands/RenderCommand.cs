using System;
using System.Drawing;
using System.Numerics;
using Vortice;
using Vortice.DCommon;
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

public class DrawBlurRectangleCommand : IRenderCommand
{
    public RawRectF Rectangle { get; set; }
    public float BlurRadius { get; set; }
    public float RadiusX { get; set; }
    public float RadiusY { get; set; }

    public void Execute(D2DRenderer renderer)
    {
        ID2D1DeviceContext context = renderer.Context;
        ID2D1Bitmap1 renderTargetBitmap = renderer.GetRenderTargetBitmap();

        if (context == null || renderTargetBitmap == null || renderer.D2DFactory == null || Rectangle.Right - Rectangle.Left <= 0 || Rectangle.Bottom - Rectangle.Top <= 0)
            return;

        float padding = BlurRadius * 3;
        var initialInflatedRect = new RawRectF(Rectangle.Left - padding, Rectangle.Top - padding, Rectangle.Right + padding, Rectangle.Bottom + padding);
        Size renderTargetSize = renderTargetBitmap.PixelSize;
        var inflatedRect = new RawRectF(
            Math.Max(0, initialInflatedRect.Left), Math.Max(0, initialInflatedRect.Top),
            Math.Min(renderTargetSize.Width, initialInflatedRect.Right), Math.Min(renderTargetSize.Height, initialInflatedRect.Bottom)
        );
        var inflatedSize = new Size((int)(inflatedRect.Right - inflatedRect.Left), (int)(inflatedRect.Bottom - inflatedRect.Top));
        if (inflatedSize.Width <= 0 || inflatedSize.Height <= 0)
            return;

        using ID2D1Bitmap1 backgroundSnapshot = CreateBackgroundSnapshot(context, renderTargetBitmap, inflatedRect, inflatedSize);
        if (backgroundSnapshot == null)
            return;

        ID2D1Effect blurEffect = renderer.GetGaussianBlurEffect();
        blurEffect.SetInput(0, backgroundSnapshot, true);
        blurEffect.SetValue((int)GaussianBlurProperties.StandardDeviation, BlurRadius);
        blurEffect.SetValue((int)GaussianBlurProperties.Optimization, GaussianBlurOptimization.Speed);

        using ID2D1Geometry geometry = CreateGeometry(renderer);

        var imageBrushProperties = new ImageBrushProperties
        {
            SourceRectangle = new RawRectF(0, 0, inflatedSize.Width, inflatedSize.Height),
            ExtendModeX = ExtendMode.Clamp, ExtendModeY = ExtendMode.Clamp,
            InterpolationMode = InterpolationMode.Linear
        };
        using (ID2D1Image finalBackgroundImage = blurEffect.Output)
        using (ID2D1ImageBrush backgroundBrush = context.CreateImageBrush(finalBackgroundImage, imageBrushProperties))
        {
            backgroundBrush.Transform = Matrix3x2.CreateTranslation(inflatedRect.Left, inflatedRect.Top);
            context.FillGeometry(geometry, backgroundBrush);
        }
    }

    private ID2D1Bitmap1 CreateBackgroundSnapshot(ID2D1DeviceContext context, ID2D1Bitmap1 renderTargetBitmap, RawRectF sourceRectD, Size sourceSize)
    {
        var bitmapProperties = new BitmapProperties1(
            new PixelFormat(renderTargetBitmap.PixelFormat.Format, renderTargetBitmap.PixelFormat.AlphaMode),
            renderTargetBitmap.Dpi.Width, renderTargetBitmap.Dpi.Height,
            BitmapOptions.Target
        );
        ID2D1Bitmap1 tempBitmap = context.CreateBitmap(sourceSize, bitmapProperties);
        var sourceRect = new Rectangle((int)sourceRectD.Left, (int)sourceRectD.Top, sourceSize.Width, sourceSize.Height);
        tempBitmap.CopyFromRenderTarget(new Point(0, 0), context, sourceRect);
        return tempBitmap;
    }

    private ID2D1Geometry CreateGeometry(D2DRenderer renderer)
    {
        if (RadiusX > 0 && RadiusY > 0)
        {
            var roundedRect = new RoundedRectangle { Rect = Rectangle, RadiusX = RadiusX, RadiusY = RadiusY };
            return renderer.D2DFactory.CreateRoundedRectangleGeometry(roundedRect);
        }

        return renderer.D2DFactory.CreateRectangleGeometry(Rectangle);
    }
}