using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Synopio.Services;

public static partial class WindowBackdrop
{
    // Constants
    private const int SystemBackdropTypeAttribute = 38;
    private const int NoBackdrop = 1;

    // Methods
    public static void Disable(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var value = NoBackdrop;
        _ = DwmSetWindowAttribute(handle, SystemBackdropTypeAttribute, ref value, sizeof(int));
    }

    // Native methods
    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
