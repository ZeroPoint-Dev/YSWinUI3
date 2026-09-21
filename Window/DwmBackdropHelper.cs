using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace YSWinUI3.Window;

internal static class DwmBackdropHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;

        public MARGINS(int uniformMargin)
        {
            cxLeftWidth = uniformMargin;
            cxRightWidth = uniformMargin;
            cyTopHeight = uniformMargin;
            cyBottomHeight = uniformMargin;
        }
    }

    private enum DWMWINDOWATTRIBUTE
    {
        DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
        DWMWA_SYSTEMBACKDROP_TYPE = 38
    }

    private enum DwmSystemBackdropType
    {
        Auto = 0,
        None = 1,
        MainWindow = 2,      // Mica
        TransientWindow = 3, // Acrylic
        TabbedWindow = 4     // Mica Alt 风格
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        DWMWINDOWATTRIBUTE dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    public static void Apply(Microsoft.UI.Xaml.Window window, BackdropKind backdropMode, bool isDark)
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
            return;

        try
        {
            var margins = new MARGINS(-1);
            _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);

            int dark = isDark ? 1 : 0;
            _ = DwmSetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref dark,
                sizeof(int));

            int type = backdropMode switch
            {
                BackdropKind.None => (int)DwmSystemBackdropType.None,
                BackdropKind.Mica => (int)DwmSystemBackdropType.MainWindow,
                BackdropKind.MicaAlt => (int)DwmSystemBackdropType.TabbedWindow,
                BackdropKind.Acrylic => (int)DwmSystemBackdropType.TransientWindow,
                _ => (int)DwmSystemBackdropType.TabbedWindow
            };

            _ = DwmSetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE,
                ref type,
                sizeof(int));
        }
        catch
        {
        }
    }
}
