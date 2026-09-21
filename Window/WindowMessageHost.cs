using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace YSWinUI3.Window;

internal sealed class WindowMessageHost : IDisposable
{
    private const int GwlStyle = -16;
    private const long WsThickFrame = 0x00040000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const uint WmGetMinMaxInfo = 0x0024;
    private const uint WmNCRButtonUp = 0x00A5;
    private const uint WmSysCommand = 0x0112;
    private const uint ScMouseMenu = 0xF090;
    private const uint ScKeyMenu = 0xF100;
    private const int HtCaption = 2;
    private const int HtSysMenu = 3;
    private const int HtMinButton = 8;
    private const int HtMaxButton = 9;
    private const int HtClose = 20;
    private static readonly UIntPtr SubclassId = (UIntPtr)0x5953;

    private readonly Microsoft.UI.Xaml.Window _window;
    private readonly DispatcherQueue _dispatcher;
    private readonly YSWindowOptions _options;
    private readonly Action<bool> _showWindowMenu;
    private SUBCLASSPROC? _proc;
    private IntPtr _hwnd;
    private bool _attached;

    public WindowMessageHost(Microsoft.UI.Xaml.Window window, DispatcherQueue dispatcher, YSWindowOptions options, Action<bool> showWindowMenu)
    {
        _window = window;
        _dispatcher = dispatcher;
        _options = options;
        _showWindowMenu = showWindowMenu;
    }

    public void Attach()
    {
        if (_attached)
            return;

        _hwnd = WindowNative.GetWindowHandle(_window);
        _proc = WindowSubclassProc;
        _attached = SetWindowSubclass(_hwnd, _proc, SubclassId, IntPtr.Zero);
    }

    private IntPtr WindowSubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data)
    {
        if (message == WmGetMinMaxInfo)
        {
            var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            uint dpi = GetDpiForWindow(hwnd);
            float scale = dpi <= 0 ? 1 : dpi / 96f;
            info.MinTrackSize.X = (int)(_options.MinimumWidth * scale);
            info.MinTrackSize.Y = (int)(_options.MinimumHeight * scale);
            Marshal.StructureToPtr(info, lParam, false);
            return IntPtr.Zero;
        }

        if (message == WmNCRButtonUp && IsTitleBarHit(wParam.ToInt32()))
        {
            _dispatcher.TryEnqueue(() => _showWindowMenu(true));
            return IntPtr.Zero;
        }

        if (message == WmSysCommand)
        {
            uint command = unchecked((uint)wParam.ToInt64()) & 0xFFF0;
            if (command == ScMouseMenu)
            {
                _dispatcher.TryEnqueue(() => _showWindowMenu(true));
                return IntPtr.Zero;
            }
            if (command == ScKeyMenu)
            {
                _dispatcher.TryEnqueue(() => _showWindowMenu(false));
                return IntPtr.Zero;
            }
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private static bool IsTitleBarHit(int hitTest) => hitTest is HtCaption or HtSysMenu or HtMinButton or HtMaxButton or HtClose;

    public void Dispose()
    {
        if (_attached && _proc is not null && _hwnd != IntPtr.Zero)
            _ = RemoveWindowSubclass(_hwnd, _proc, SubclassId);
        _attached = false;
        _proc = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT Reserved;
        public POINT MaxSize;
        public POINT MaxPosition;
        public POINT MinTrackSize;
        public POINT MaxTrackSize;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SUBCLASSPROC(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, IntPtr data);

    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool SetWindowSubclass(IntPtr hwnd, SUBCLASSPROC proc, UIntPtr id, IntPtr data);
    [DllImport("comctl32.dll", SetLastError = true)] private static extern bool RemoveWindowSubclass(IntPtr hwnd, SUBCLASSPROC proc, UIntPtr id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
}
