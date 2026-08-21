using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MaxLight;

//через этот класс определяем на каком мониторе запущена программа.
// Например, чтобы на нем же показывать пуши
public static class ScreenHelper
{
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    public class ScreenInfo
    {
        public Rect WorkingArea { get; set; }
        public Rect Bounds { get; set; }
    }

    public static ScreenInfo GetCurrentScreen(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var monitorHandle = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);

        var info = new MONITORINFO();
        info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        GetMonitorInfo(monitorHandle, ref info);

        return new ScreenInfo
        {
            WorkingArea = new Rect(
                info.rcWork.Left, info.rcWork.Top,
                info.rcWork.Right - info.rcWork.Left,
                info.rcWork.Bottom - info.rcWork.Top),
            Bounds = new Rect(
                info.rcMonitor.Left, info.rcMonitor.Top,
                info.rcMonitor.Right - info.rcMonitor.Left,
                info.rcMonitor.Bottom - info.rcMonitor.Top)
        };
    }

    public static bool IsPositionOnScreen(int left, int top)
    {
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        return left >= virtualScreen.Left - 100 &&
               top >= virtualScreen.Top - 100 &&
               left < virtualScreen.Right + 100 &&
               top < virtualScreen.Bottom + 100;
    }
}