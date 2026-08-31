using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace MaxLight;

/// <summary>
/// Помогает ресайзить окно без рамки
/// </summary>
public class WindowResizeHelper
{
    private readonly Window _window;
    private const int BorderSize = 6;

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_TOP = 12, HT_TOPLEFT = 13, HT_TOPRIGHT = 14;
    private const int HT_BOTTOM = 15, HT_BOTTOMLEFT = 16, HT_BOTTOMRIGHT = 17;
    private const int HT_LEFT = 10, HT_RIGHT = 11;

    public WindowResizeHelper(Window window)
    {
        _window = window;
    }

    public void HandleResize(IntPtr handle, MouseButtonState leftButton, Point mousePosition)
    {
        if (leftButton != MouseButtonState.Pressed) return;
        

        int hit = GetResizeDirection(mousePosition);
        if (hit != 0)
        {
            ReleaseCapture();
            SendMessage(handle, WM_NCLBUTTONDOWN, (IntPtr)hit, IntPtr.Zero);
        }
    }

    private int GetResizeDirection(Point mousePosition)
    {
        double width = _window.Width;
        double height = _window.Height;

        bool isLeft = mousePosition.X <= BorderSize;
        bool isRight = mousePosition.X >= width - BorderSize;
        bool isTop = mousePosition.Y <= BorderSize;
        bool isBottom = mousePosition.Y >= height - BorderSize;

        if (isTop && isLeft) return HT_TOPLEFT;
        if (isTop && isRight) return HT_TOPRIGHT;
        if (isBottom && isLeft) return HT_BOTTOMLEFT;
        if (isBottom && isRight) return HT_BOTTOMRIGHT;
        if (isTop) return HT_TOP;
        if (isBottom) return HT_BOTTOM;
        if (isLeft) return HT_LEFT;
        if (isRight) return HT_RIGHT;

        return 0;
    }

    public Cursor GetResizeCursor(Point mousePosition)
    {
        double width = _window.Width;
        double height = _window.Height;

        bool isLeft = mousePosition.X <= BorderSize;
        bool isRight = mousePosition.X >= width - BorderSize;
        bool isTop = mousePosition.Y <= BorderSize;
        bool isBottom = mousePosition.Y >= height - BorderSize;

        if ((isTop && isLeft) || (isBottom && isRight)) return Cursors.SizeNWSE;
        if ((isTop && isRight) || (isBottom && isLeft)) return Cursors.SizeNESW;
        if (isTop || isBottom) return Cursors.SizeNS;
        if (isLeft || isRight) return Cursors.SizeWE;

        return Cursors.Arrow;
    }
}