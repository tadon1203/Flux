using System;
using System.Collections.Generic;
using System.Numerics;
using Flux.Graphics.Commands;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using AlphaMode = Vortice.DCommon.AlphaMode;

namespace Flux.Graphics;

/// <summary>
///     Manages Direct2D rendering, resources, and command queuing.
/// </summary>
public class D2DRenderer : IDisposable
{
    public static D2DRenderer Instance { get; set; }

    private List<IRenderCommand> _commandsToRender = new();
    private readonly object _commandLock = new();

    private IDWriteFactory _dwriteFactory;
    private ID2D1Device _d2dDevice;
    private ID2D1Bitmap1 _d2dRenderTarget;

    private readonly Dictionary<Color4, ID2D1SolidColorBrush> _brushCache = new();
    private readonly Dictionary<string, IDWriteTextFormat> _textFormatCache = new();

    public ID2D1DeviceContext Context { get; private set; }
    public ID2D1Factory1 D2DFactory { get; private set; }

    public void QueueCommands(IEnumerable<IRenderCommand> commands)
    {
        lock (_commandLock)
        {
            _commandsToRender = new List<IRenderCommand>(commands);
        }
    }

    public void Initialize(IDXGISwapChain swapChain)
    {
        D2DFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>();
        _dwriteFactory = DWrite.DWriteCreateFactory<IDWriteFactory>();
        using var dxgiDevice = swapChain.GetDevice<IDXGIDevice>();
        _d2dDevice = D2DFactory.CreateDevice(dxgiDevice);
        Context = _d2dDevice.CreateDeviceContext();

        UpdateRenderTarget(swapChain);
        Logger.Debug("D2D Renderer initialized.");
    }

    public void UpdateRenderTarget(IDXGISwapChain swapChain)
    {
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

    public void Render()
    {
        if (Context == null)
            return;

        Context.BeginDraw();
        Context.Transform = Matrix3x2.Identity;

        lock (_commandLock)
        {
            foreach (IRenderCommand command in _commandsToRender)
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

        lock (_commandLock)
        {
            _commandsToRender.Clear();
        }

        _d2dRenderTarget?.Dispose();
        Context?.Dispose();
        _d2dDevice?.Dispose();
        _dwriteFactory?.Dispose();
        D2DFactory?.Dispose();

        _d2dRenderTarget = null;
        Context = null;
        _d2dDevice = null;
        _dwriteFactory = null;
        D2DFactory = null;

        if (Instance == this)
            Instance = null;
    }
}