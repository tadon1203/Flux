using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using Flux.Core;
using Flux.Graphics.Commands;
using Vortice;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
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

    public static void DrawText(string text, string fontFamily, float fontSize, Vector2 position, Color4 color)
    {
        Instance?.EnqueueDrawText(text, fontFamily, fontSize, position, color);
    }

    public static void DrawRectangle(RawRectF rect, Color4 color, float strokeWidth = 1.0f)
    {
        Instance?.EnqueueDrawRectangle(rect, color, strokeWidth);
    }

    public static void FillRectangle(RawRectF rect, Color4 color)
    {
        Instance?.EnqueueFillRectangle(rect, color);
    }

    public static void DrawAcrylicRectangle(RawRectF rect, float blurRadius, float radiusX, float radiusY)
    {
        var defaultTintColor = new Color4(0.05f, 0.05f, 0.05f, 0.2f);
        const float defaultSaturation = 1.25f;
        const float defaultNoiseOpacity = 0.02f;

        Instance?.EnqueueDrawAcrylicRectangle(rect, blurRadius, defaultTintColor, defaultSaturation, defaultNoiseOpacity, radiusX, radiusY);
    }

    public static void DrawAcrylicRectangle(RawRectF rect, float blurRadius, Color4 tintColor, float saturation = 1.25f, float noiseOpacity = 0.02f, float radiusX = 0, float radiusY = 0)
    {
        Instance?.EnqueueDrawAcrylicRectangle(rect, blurRadius, tintColor, saturation, noiseOpacity, radiusX, radiusY);
    }

    #endregion

    private IDWriteFactory _dwriteFactory;
    private ID2D1Device _d2dDevice;
    private ID2D1Bitmap1 _d2dRenderTarget;

    private ID2D1Effect _gaussianBlurEffect;
    private ID2D1Effect _saturationEffect;
    private ID2D1Effect _floodEffect;
    private ID2D1Effect _compositeEffect;

    private ID2D1Bitmap _noiseTexture;
    private ID2D1BitmapBrush _noiseBrush;

    private readonly ConcurrentQueue<IRenderCommand> _renderQueue = new();
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
        _gaussianBlurEffect = new GaussianBlur(Context);
        _saturationEffect = new Saturation(Context);
        _floodEffect = new Flood(Context);
        _compositeEffect = new Composite(Context);
        CreateNoiseTexture();

        UpdateRenderTarget(swapChain);
        Logger.Debug("D2D Renderer initialized.");
    }

    private void CreateNoiseTexture()
    {
        const int noiseSize = 128;
        int[] noisePixels = new int[noiseSize * noiseSize];
        var random = new Random();

        for (int i = 0; i < noisePixels.Length; i++)
        {
            // Generate subtle grayscale noise
            byte value = (byte)random.Next(40, 60);
            noisePixels[i] = (255 << 24) | (value << 16) | (value << 8) | value; // 0xAARRGGBB for B8G8R8A8 format
        }

        // Pin the array in memory to get a stable pointer.
        GCHandle handle = GCHandle.Alloc(noisePixels, GCHandleType.Pinned);
        try
        {
            IntPtr dataPtr = handle.AddrOfPinnedObject();
            Vector2 dpi = D2DFactory.DesktopDpi;

            // Use BitmapProperties1 as required by ID2D1DeviceContext.CreateBitmap
            var bitmapProperties = new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Ignore),
                dpi.X, dpi.Y,
                BitmapOptions.None
            );

            var size = new Size(noiseSize, noiseSize);
            int pitch = noiseSize * 4; // bytes per row

            _noiseTexture = Context.CreateBitmap(size, dataPtr, pitch, bitmapProperties);
        }
        finally
        {
            handle.Free();
        }

        var brushProperties = new BitmapBrushProperties
        {
            ExtendModeX = ExtendMode.Wrap,
            ExtendModeY = ExtendMode.Wrap,
            InterpolationMode = BitmapInterpolationMode.NearestNeighbor
        };
        _noiseBrush = Context.CreateBitmapBrush(_noiseTexture, brushProperties);
    }

    private void UpdateRenderTarget(IDXGISwapChain swapChain)
    {
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

    private void Enqueue(IRenderCommand command)
    {
        _renderQueue.Enqueue(command);
    }

    #region Instance Drawing Helpers (private)

    private void EnqueueDrawText(string text, string fontFamily, float fontSize, Vector2 position, Color4 color)
    {
        Enqueue(new DrawTextCommand { Text = text, FontFamily = fontFamily, FontSize = fontSize, Position = position, Color = color });
    }

    private void EnqueueDrawRectangle(RawRectF rect, Color4 color, float strokeWidth = 1.0f)
    {
        Enqueue(new DrawRectangleCommand { Rectangle = rect, Color = color, StrokeWidth = strokeWidth });
    }

    private void EnqueueFillRectangle(RawRectF rect, Color4 color)
    {
        Enqueue(new FillRectangleCommand { Rectangle = rect, Color = color });
    }

    private void EnqueueDrawAcrylicRectangle(RawRectF rect, float blurRadius, Color4 tintColor, float saturation, float noiseOpacity, float radiusX, float radiusY)
    {
        Enqueue(new DrawAcrylicRectangleCommand
        {
            Rectangle = rect,
            BlurRadius = blurRadius,
            TintColor = tintColor,
            Saturation = saturation,
            NoiseOpacity = noiseOpacity,
            RadiusX = radiusX,
            RadiusY = radiusY
        });
    }

    #endregion

    /// <summary>
    ///     Executes all queued render commands.
    /// </summary>
    public void Render()
    {
        if (Context == null)
            return;
        Context.BeginDraw();
        Context.Transform = Matrix3x2.Identity;
        while (_renderQueue.TryDequeue(out IRenderCommand command))
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

    public ID2D1Effect GetGaussianBlurEffect()
    {
        return _gaussianBlurEffect;
    }

    public ID2D1Effect GetSaturationEffect()
    {
        return _saturationEffect;
    }

    public ID2D1Effect GetFloodEffect()
    {
        return _floodEffect;
    }

    public ID2D1Effect GetCompositeEffect()
    {
        return _compositeEffect;
    }

    public ID2D1BitmapBrush GetNoiseBrush()
    {
        return _noiseBrush;
    }

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

        _noiseBrush?.Dispose();
        _noiseTexture?.Dispose();

        _compositeEffect?.Dispose();
        _floodEffect?.Dispose();
        _saturationEffect?.Dispose();
        _gaussianBlurEffect?.Dispose();

        _d2dRenderTarget?.Dispose();
        Context?.Dispose();
        _d2dDevice?.Dispose();
        _dwriteFactory?.Dispose();
        D2DFactory?.Dispose();

        _noiseBrush = null;
        _noiseTexture = null;

        _compositeEffect = null;
        _floodEffect = null;
        _saturationEffect = null;
        _gaussianBlurEffect = null;

        _d2dRenderTarget = null;
        Context = null;
        _d2dDevice = null;
        _dwriteFactory = null;
        D2DFactory = null;

        if (Instance == this)
            Instance = null;
    }
}