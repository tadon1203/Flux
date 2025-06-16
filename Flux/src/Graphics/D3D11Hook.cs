using System;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Flux.Core.Interop.Win32;

namespace Flux.Graphics;

public static class D3D11Hook
{
    private const int PresentVTableIndex = 8;
    private const int ResizeBuffersVTableIndex = 13;

    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
    private delegate Result PresentDelegate(IntPtr swapChainPtr, int syncInterval, PresentFlags flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
    private delegate Result ResizeBuffersDelegate(IntPtr swapChainPtr, int bufferCount, int width, int height, Format newFormat, SwapChainFlags swapChainFlags);

    private static PresentDelegate _originalPresent;
    private static ResizeBuffersDelegate _originalResizeBuffers;

    private static PresentDelegate _hookedPresentDelegate;
    private static ResizeBuffersDelegate _hookedResizeBuffersDelegate;

    private static IntPtr _swapChainVTable;
    private static bool _isRendererInitialized;
    private static bool _wasResized;

    public static bool Hook()
    {
        IntPtr hWnd = IntPtr.Zero;
        IDXGISwapChain swapChain = null;
        ID3D11Device device = null;
        ID3D11DeviceContext context = null;

        try
        {
            hWnd = CreateDummyWindow();
            if (hWnd == IntPtr.Zero)
            {
                Logger.Error($"Failed to create dummy window. Error: {Marshal.GetLastWin32Error()}");
                return false;
            }

            var swapChainDescription = new SwapChainDescription
            {
                BufferCount = 1,
                BufferDescription = new ModeDescription(100, 100, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                BufferUsage = Usage.RenderTargetOutput,
                OutputWindow = hWnd,
                SampleDescription = new SampleDescription(1, 0),
                Windowed = true
            };

            Result result = D3D11.D3D11CreateDeviceAndSwapChain(null, DriverType.Hardware, DeviceCreationFlags.None, null!, swapChainDescription, out swapChain, out device, out _, out context);
            if (result.Failure)
            {
                Logger.Error($"D3D11CreateDeviceAndSwapChain failed: {result.Description}");
                return false;
            }

            if (!InstallVTableHooks(swapChain))
                return false;

            Logger.Info("D3D11 hooks installed successfully.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"An unexpected error occurred during D3D11 hooking: {ex}");
            return false;
        }
        finally
        {
            context?.Dispose();
            device?.Dispose();
            swapChain?.Dispose();
            if (hWnd != IntPtr.Zero)
                DestroyWindow(hWnd);
        }
    }
    
    private static IntPtr CreateDummyWindow()
    {
        const string className = "FluxDummyWindow";
        var wndClass = new WNDCLASS
        {
            lpfnWndProc = DefWindowProc,
            lpszClassName = className,
            hInstance = GetModuleHandle(null)
        };

        // 1410: ERROR_CLASS_ALREADY_EXISTS (safe to ignore)
        if (RegisterClass(ref wndClass) == 0 && Marshal.GetLastWin32Error() != 1410)
            return IntPtr.Zero;

        return CreateWindowEx(0, className, "Dummy Window", 0, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, wndClass.hInstance, IntPtr.Zero);
    }

    private static bool InstallVTableHooks(IDXGISwapChain swapChain)
    {
        _swapChainVTable = Marshal.ReadIntPtr(swapChain.NativePointer);
        IntPtr originalPresentPtr = Marshal.ReadIntPtr(_swapChainVTable, PresentVTableIndex * IntPtr.Size);
        IntPtr originalResizeBuffersPtr = Marshal.ReadIntPtr(_swapChainVTable, ResizeBuffersVTableIndex * IntPtr.Size);

        _originalPresent = Marshal.GetDelegateForFunctionPointer<PresentDelegate>(originalPresentPtr);
        _originalResizeBuffers = Marshal.GetDelegateForFunctionPointer<ResizeBuffersDelegate>(originalResizeBuffersPtr);

        _hookedPresentDelegate = HookedPresent;
        _hookedResizeBuffersDelegate = HookedResizeBuffers;
        IntPtr hookedPresentPtr = Marshal.GetFunctionPointerForDelegate(_hookedPresentDelegate);
        IntPtr hookedResizeBuffersPtr = Marshal.GetFunctionPointerForDelegate(_hookedResizeBuffersDelegate);

        int maxIndex = Math.Max(PresentVTableIndex, ResizeBuffersVTableIndex);
        var protectionSize = (UIntPtr)((maxIndex + 1) * IntPtr.Size);

        if (VirtualProtect(_swapChainVTable, protectionSize, MemoryProtection.PageExecuteReadWrite, out MemoryProtection oldProtect))
        {
            Marshal.WriteIntPtr(_swapChainVTable, PresentVTableIndex * IntPtr.Size, hookedPresentPtr);
            Marshal.WriteIntPtr(_swapChainVTable, ResizeBuffersVTableIndex * IntPtr.Size, hookedResizeBuffersPtr);
            VirtualProtect(_swapChainVTable, protectionSize, oldProtect, out _);
            return true;
        }

        Logger.Error($"VirtualProtect failed. Error: {Marshal.GetLastWin32Error()}");
        return false;
    }

    private static Result HookedPresent(IntPtr swapChainPtr, int syncInterval, PresentFlags flags)
    {
        // DO NOT use 'using' as we don't own this object.
        var swapChain = new IDXGISwapChain(swapChainPtr);

        try
        {
            if (!_isRendererInitialized)
            {
                D2DRenderer.Instance = new D2DRenderer();
                D2DRenderer.Instance.Initialize(swapChain);
                _isRendererInitialized = true;
                
                if (_wasResized)
                {
                    Logger.Info("D2D Renderer re-initialized after resize.");
                    _wasResized = false;
                }
                else
                {
                    Logger.Info("D2D Renderer initialized.");
                }
            }
            else
            {
                D2DRenderer.Instance?.UpdateRenderTarget(swapChain);
            }

            D2DRenderer.Instance?.Render();
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in HookedPresent: {ex}");
            D2DRenderer.Instance?.Dispose();
            _isRendererInitialized = false;
        }

        return _originalPresent(swapChainPtr, syncInterval, flags);
    }

    private static Result HookedResizeBuffers(IntPtr swapChainPtr, int bufferCount, int width, int height, Format newFormat, SwapChainFlags swapChainFlags)
    {
        Logger.Debug("ResizeBuffers called. Disposing old D2D resources.");
        D2DRenderer.Instance?.Dispose();
        _isRendererInitialized = false;
        _wasResized = true;

        Result result = _originalResizeBuffers(swapChainPtr, bufferCount, width, height, newFormat, swapChainFlags);
        if (!result.Success)
            Logger.Warning($"Original ResizeBuffers failed with result: {result.Description}");

        return result;
    }
}