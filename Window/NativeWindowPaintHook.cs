using System;
using System.Runtime.InteropServices;

namespace YSWinUI3.Window;

internal sealed class NativeWindowPaintHook : IDisposable
{
    private readonly IntPtr _hwnd;
    private WndProcDelegate? _newWndProc;
    private IntPtr _oldWndProc;

    private const int GWLP_WNDPROC = -4;

    private const uint WM_PAINT = 0x000F;
    private const uint WM_ERASEBKGND = 0x0014;

    public NativeWindowPaintHook(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    public void Attach()
    {
        if (_hwnd == IntPtr.Zero || _oldWndProc != IntPtr.Zero)
            return;

        _newWndProc = WndProc;
        IntPtr procPtr = Marshal.GetFunctionPointerForDelegate(_newWndProc);
        _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, procPtr);
    }

    public void Detach()
    {
        if (_hwnd == IntPtr.Zero || _oldWndProc == IntPtr.Zero)
            return;

        SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _oldWndProc);
        _oldWndProc = IntPtr.Zero;
        _newWndProc = null;
    }

    public void Dispose() => Detach();

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_ERASEBKGND:
                // 拦截背景擦除，阻止系统刷默认白底
                return new IntPtr(1);

            case WM_PAINT:
                {
                    PAINTSTRUCT ps;
                    IntPtr hdc = BeginPaint(hWnd, out ps);

                    GetClientRect(hWnd, out RECT rcClient);

                    IntPtr hBrush = CreateSolidBrush(MakeRgb(0, 0, 0));
                    FillRect(hdc, ref rcClient, hBrush);
                    DeleteObject(hBrush);

                    EndPaint(hWnd, ref ps);

                    // 表示我们已经处理完绘制
                    return IntPtr.Zero;
                }
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private static uint MakeRgb(byte r, byte g, byte b)
    {
        return (uint)(r | (g << 8) | (b << 16));
    }

    private delegate IntPtr WndProcDelegate(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore;
        public int fIncUpdate;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern IntPtr CallWindowProc(
        IntPtr lpPrevWndFunc,
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(uint colorRef);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);

        return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }
}
