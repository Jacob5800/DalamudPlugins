using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TataruBrawler;

// Window-directed messages never use the foreground application's keyboard queue.
internal sealed class BackgroundKeys
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessageW(nint window, uint message, nuint key, nint data);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern short VkKeyScanEx(char key, nint layout);
    [DllImport("user32.dll")] private static extern uint MapVirtualKeyEx(uint code, uint type, nint layout);
    private nint window, heldWindow, heldData;
    private ushort held;
    private long releaseAt;
    public bool Ready => held == 0;
    public int LastError { get; private set; }

    private bool OwnWindow(nint handle)
    {
        if (handle == 0 || !IsWindow(handle)) return false;
        GetWindowThreadProcessId(handle, out uint owner);
        return owner == Environment.ProcessId;
    }

    public bool Release(bool force = false)
    {
        if (held == 0 || (!force && Environment.TickCount64 < releaseAt)) return true;
        bool valid = OwnWindow(heldWindow);
        bool ok = !valid || PostMessageW(heldWindow, 0x101, held, (nint)(heldData.ToInt64() | 0xC0000000L));
        LastError = ok ? 0 : Marshal.GetLastWin32Error();
        if (ok) { held = 0; heldWindow = heldData = 0; }
        return ok;
    }

    public bool Send(char key)
    {
        if (!Ready || !BackgroundKeyPolicy.Allowed(key)) return false;
        if (!OwnWindow(window))
        {
            using var process = Process.GetCurrentProcess();
            window = process.MainWindowHandle;
            if (!OwnWindow(window)) return false;
        }
        uint thread = GetWindowThreadProcessId(window, out _);
        nint layout = GetKeyboardLayout(thread);
        short mapping = key == '\r' ? (short)0x0D : key == '\b' ? (short)0x08 : VkKeyScanEx(char.ToLowerInvariant(key), layout);
        // Shift/AltGr punctuation goes directly to this window as a character;
        // do not alter global modifier state while another application is active.
        if (mapping == -1 || (mapping & 0xFF00) != 0)
        {
            bool posted = PostMessageW(window, 0x102, key, 1);
            LastError = posted ? 0 : Marshal.GetLastWin32Error();
            return posted;
        }
        ushort vk = (ushort)(mapping & 255);
        uint scan = MapVirtualKeyEx(vk, 0, layout);
        if (scan == 0) return false;
        nint data = (nint)(1L | ((long)scan << 16));
        if (!PostMessageW(window, 0x100, vk, data))
        {
            LastError = Marshal.GetLastWin32Error();
            return false;
        }
        held = vk;
        heldWindow = window;
        heldData = data;
        releaseAt = Environment.TickCount64 + 30;
        LastError = 0;
        return true;
    }
}
