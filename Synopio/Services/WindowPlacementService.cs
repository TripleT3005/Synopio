using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

namespace Synopio.Services;

public sealed partial class WindowPlacementService
{
    // Constants
    private const int ShowNormal = 1;
    private const int ShowMaximized = 3;
    private const uint MonitorDefaultToNull = 0;
    private const int MinimumVisibleSize = 48;

    // Fields
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Synopio", "window.json");

    // Methods
    public void Restore(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var stored = Load();
        if (stored == null || !IsVisibleOnConnectedMonitor(stored))
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        var placement = new NativePlacement
        {
            Length = Marshal.SizeOf<NativePlacement>(),
            ShowCommand = stored.Maximized ? ShowMaximized : ShowNormal,
            NormalPosition = new NativeRect { Left = stored.Left, Top = stored.Top, Right = stored.Right, Bottom = stored.Bottom },
        };

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        SetWindowPlacement(handle, ref placement);
        SetWindowPlacement(handle, ref placement);
    }

    public void Save(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).Handle;
        var placement = new NativePlacement { Length = Marshal.SizeOf<NativePlacement>() };
        if (handle == IntPtr.Zero || !GetWindowPlacement(handle, ref placement))
        {
            return;
        }

        var rect = placement.NormalPosition;
        var stored = new StoredPlacement(rect.Left, rect.Top, rect.Right, rect.Bottom, placement.ShowCommand == ShowMaximized);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(stored, JsonOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }
    }

    private StoredPlacement? Load()
    {
        try
        {
            return File.Exists(_path) ? JsonSerializer.Deserialize<StoredPlacement>(File.ReadAllText(_path)) : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsVisibleOnConnectedMonitor(StoredPlacement placement)
    {
        if (placement.Right - placement.Left < MinimumVisibleSize || placement.Bottom - placement.Top < MinimumVisibleSize)
        {
            return false;
        }

        var titleBar = new NativeRect
        {
            Left = placement.Left + MinimumVisibleSize,
            Top = placement.Top,
            Right = placement.Right - MinimumVisibleSize,
            Bottom = placement.Top + MinimumVisibleSize,
        };

        return MonitorFromRect(ref titleBar, MonitorDefaultToNull) != IntPtr.Zero;
    }

    // Native methods
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowPlacement(IntPtr handle, ref NativePlacement placement);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPlacement(IntPtr handle, ref NativePlacement placement);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromRect(ref NativeRect rect, uint flags);

    // Types
    private sealed record StoredPlacement(int Left, int Top, int Right, int Bottom, bool Maximized);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePlacement
    {
        public int Length;
        public int Flags;
        public int ShowCommand;
        public NativePoint MinPosition;
        public NativePoint MaxPosition;
        public NativeRect NormalPosition;
    }
}
