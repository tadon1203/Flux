using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Flux.Graphics.Commands;
using Vortice;
using Vortice.DCommon;
using Vortice.Direct2D1;
// using Vortice.Direct2D1.Effects; // Commented out to avoid D2DERR_EFFECT_IS_NOT_REGISTERED on Proton.
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using AlphaMode = Vortice.DCommon.AlphaMode;

namespace Flux.Graphics;

/// <summary>
///     Manages Direct2D rendering, resources, and command queuing thread-safely.
///     Provides a static API for drawing commands.
/// </summary>
public class D2DRenderer : IDisposable
{
    #region Singleton and Static API

    /// <summary>
    ///     Gets or sets the singleton instance of the D2DRenderer.
    /// </summary>
    public static D2DRenderer Instance { get; set; }
    
    private static readonly ConcurrentQueue<IRenderCommand> _commandQueue = new();

    public static void DrawText(string text, string fontFamily, float fontSize, Vector2 position, Color4 color)
    {
        _commandQueue.Enqueue(new DrawTextCommand { Text = text, FontFamily = fontFamily, FontSize = fontSize, Position = position, Color = color });
    }

    public static void DrawRectangle(RawRectF rect, Color4 color, float strokeWidth = 1.0f)
    {
        _commandQueue.Enqueue(new DrawRectangleCommand { Rectangle = rect, Color = color, StrokeWidth = strokeWidth });
    }

    public static void FillRectangle(RawRectF rect, Color4 color)
    {
        _commandQueue.Enqueue(new FillRectangleCommand { Rectangle = rect, Color = color });
    }

    /*
     * Commented out to avoid D2DERR_EFFECT_IS_NOT_REGISTERED.
     * This error can occur in environments like Proton where CLSID_D2D1GaussianBlur might not be supported.
    public static void DrawBlurRectangle(RawRectF rect, float blurRadius, float radiusX = 0, float radiusY = 0)
    {
        // If re-enabling, change this to enqueue to the static _commandQueue as well.
        _commandQueue.Enqueue(new DrawBlurRectangleCommand
        {
            Rectangle = rect,
            BlurRadius = blurRadius,
            RadiusX = radiusX,
            RadiusY = radiusY
        });
    }
    */

    #endregion

    private IDWriteFactory _dwriteFactory;
    private ID2D1Device _d2dDevice;
    private ID2D1Bitmap1 _d2dRenderTarget;

    // private ID2D1Effect _gaussianBlurEffect; // Commented out to avoid D2DERR_EFFECT_IS_NOT_REGISTERED on Proton.

    private readonly Dictionary<Color4, ID2D1SolidColorBrush> _brushCache = new();
    private readonly Dictionary<string, IDWriteTextFormat> _textFormatCache = new();

    public ID2D1DeviceContext Context { get; private set; }
    public ID2D1Factory1 D2DFactory { get; private set; }

    /// <summary>
    ///     Initializes the D2D renderer and its resources using the provided swap chain.
    /// </summary>
    public void Initialize(IDXGISwapChain swapChain)
    {
        D2DFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>();
        _dwriteFactory = DWrite.DWriteCreateFactory<IDWriteFactory>();
        using var dxgiDevice = swapChain.GetDevice<IDXGIDevice>();
        _d2dDevice = D2DFactory.CreateDevice(dxgiDevice);
        Context = _d2dDevice.CreateDeviceContext();

        // Create effects and resources
        // _gaussianBlurEffect = new GaussianBlur(Context); // Commented out to avoid D2DERR_EFFECT_IS_NOT_REGISTERED on Proton.

        UpdateRenderTarget(swapChain);
        Logger.Debug("D2D Renderer initialized.");
    }

    /// <summary>
    ///     Re-creates the D2D render target from the swap chain's current back buffer.
    ///     This must be called before rendering each frame to handle swapped buffers and prevent flickering.
    /// </summary>
    public void UpdateRenderTarget(IDXGISwapChain swapChain)
    {
        // Release the reference to the old render target bitmap before disposing it.
        Context.Target = null;
        _d2dRenderTarget?.Dispose();

        Vector2 dpi = D2DFactory.DesktopDpi;
        var bitmapProperties = new BitmapProperties1(
            new PixelFormat(Format.R8G8B8A8_UNorm, AlphaMode.Premultiplied),
            dpi.X, dpi.Y,
            BitmapOptions.Target | BitmapOptions.CannotDraw
        );
        using var dxgiBackBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
        using var dxgiSurface = dxgiBackBuffer.QueryInterface<IDXGISurface>();
        _d2dRenderTarget = Context.CreateBitmapFromDxgiSurface(dxgiSurface, bitmapProperties);
        Context.Target = _d2dRenderTarget;
    }

    /// <summary>
    ///     Executes all queued render commands.
    /// </summary>
    public void Render()
    {
        if (Context == null)
            return;
        Context.BeginDraw();
        Context.Transform = Matrix3x2.Identity;
        
        while (_commandQueue.TryDequeue(out IRenderCommand command))
        {
            try
            {
                command.Execute(this);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error executing render command: {ex.Message}");
            }
        }

        Context.EndDraw();
    }

    public ID2D1SolidColorBrush GetOrCreateBrush(Color4 color)
    {
        if (_brushCache.TryGetValue(color, out ID2D1SolidColorBrush brush))
            return brush;
        brush = Context.CreateSolidColorBrush(color);
        _brushCache[color] = brush;
        return brush;
    }

    public IDWriteTextFormat GetOrCreateTextFormat(string fontFamily, float fontSize)
    {
        string key = $"{fontFamily}:{fontSize}";
        if (_textFormatCache.TryGetValue(key, out IDWriteTextFormat format))
            return format;
        format = _dwriteFactory.CreateTextFormat(fontFamily, FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, fontSize);
        _textFormatCache[key] = format;
        return format;
    }

    public ID2D1Bitmap1 GetRenderTargetBitmap()
    {
        return _d2dRenderTarget;
    }

    /*
     * Commented out to avoid D2DERR_EFFECT_IS_NOT_REGISTERED.
     * This error can occur in environments like Proton where CLSID_D2D1GaussianBlur might not be supported.
    public ID2D1Effect GetGaussianBlurEffect()
    {
        return _gaussianBlurEffect;
    }
    */

    /// <summary>
    ///     Disposes all managed and unmanaged D2D resources.
    /// </summary>
    public void Dispose()
    {
        Logger.Debug("Disposing D2D Renderer resources.");
        foreach (ID2D1SolidColorBrush brush in _brushCache.Values)
        {
            brush.Dispose();
        }

        _brushCache.Clear();
        foreach (IDWriteTextFormat format in _textFormatCache.Values)
        {
            format.Dispose();
        }

        _textFormatCache.Clear();

        // _gaussianBlurEffect?.Dispose(); // Commented out to avoid D2DERR_EFFECT_IS_NOT_REGISTERED on Proton.

        _d2dRenderTarget?.Dispose();
        Context?.Dispose();
        _d2dDevice?.Dispose();
        _dwriteFactory?.Dispose();
        D2DFactory?.Dispose();

        // _gaussianBlurEffect = null; // Commented out to avoid D2DERR_EFFECT_IS_NOT_REGISTERED on Proton.

        _d2dRenderTarget = null;
        Context = null;
        _d2dDevice = null;
        _dwriteFactory = null;
        D2DFactory = null;

        if (Instance == this)
            Instance = null;
    }
}