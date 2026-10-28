using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace TataruBrawler;

internal static class Keyboard
{
	private struct Input
	{
		public uint Type;

		public InputUnion Data;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct InputUnion
	{
		[FieldOffset(0)]
		public KeyboardInput Keyboard;

		[FieldOffset(0)]
		public MouseInput Mouse;
	}

	private struct KeyboardInput
	{
		public ushort VirtualKey;

		public ushort Scan;

		public uint Flags;

		public uint Time;

		public nuint ExtraInfo;
	}

	private struct MouseInput
	{
		public int X;

		public int Y;

		public uint MouseData;

		public uint Flags;

		public uint Time;

		public nuint ExtraInfo;
	}

	private static ushort held;

	private static bool heldShift;

	private static long releaseAt;

	private static int releaseFailures;

	public static int InputSize => Marshal.SizeOf<Input>();

	public static bool ForegroundGame
	{
		get
		{
			GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
			return pid == (uint)Environment.ProcessId;
		}
	}

	public static bool Escape => (GetAsyncKeyState(27) & 0x8000) != 0;

	public static bool Modifier
	{
		get
		{
			if (!new int[5] { 17, 18, 91, 92, 161 }.Any((int k) => (GetAsyncKeyState(k) & 0x8000) != 0))
			{
				if (!heldShift)
				{
					return (GetAsyncKeyState(160) & 0x8000) != 0;
				}
				return false;
			}
			return true;
		}
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint SendInput(uint count, Input[] inputs, int size);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

	[DllImport("user32.dll")]
	private static extern uint MapVirtualKeyEx(uint code, uint mapType, nint layout);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern short VkKeyScanEx(char c, nint layout);

	[DllImport("user32.dll")]
	private static extern nint GetKeyboardLayout(uint threadId);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int key);

	internal static bool SupportedModifiers(short mapping)
	{
		bool flag = mapping != -1;
		if (flag)
		{
			int num = mapping & 0xFF00;
			bool flag2 = ((num == 0 || num == 256) ? true : false);
			flag = flag2;
		}
		return flag;
	}

	public static bool TryMap(char c, out ushort scan)
	{
		bool shift;
		return TryMap(c, out scan, out shift);
	}

	private static bool TryMap(char c, out ushort scan, out bool shift)
	{
		scan = 0;
		shift = false;
		if (!ForegroundGame)
		{
			return false;
		}
		bool flag;
		switch (c)
		{
		case '\b':
			scan = 14;
			return true;
		case '\u0006':
			scan = 64;
			return true;
		case '\r':
			scan = 28;
			return true;
		default:
			flag = true;
			break;
		case ' ':
		case '!':
		case '"':
		case '#':
		case '$':
		case '%':
		case '&':
		case '\'':
		case '(':
		case ')':
		case '*':
		case '+':
		case ',':
		case '-':
		case '.':
		case '/':
		case '0':
		case '1':
		case '2':
		case '3':
		case '4':
		case '5':
		case '6':
		case '7':
		case '8':
		case '9':
		case ':':
		case ';':
		case '<':
		case '=':
		case '>':
		case '?':
		case '@':
		case 'A':
		case 'B':
		case 'C':
		case 'D':
		case 'E':
		case 'F':
		case 'G':
		case 'H':
		case 'I':
		case 'J':
		case 'K':
		case 'L':
		case 'M':
		case 'N':
		case 'O':
		case 'P':
		case 'Q':
		case 'R':
		case 'S':
		case 'T':
		case 'U':
		case 'V':
		case 'W':
		case 'X':
		case 'Y':
		case 'Z':
		case '[':
		case '\\':
		case ']':
		case '^':
		case '_':
		case '`':
		case 'a':
		case 'b':
		case 'c':
		case 'd':
		case 'e':
		case 'f':
		case 'g':
		case 'h':
		case 'i':
		case 'j':
		case 'k':
		case 'l':
		case 'm':
		case 'n':
		case 'o':
		case 'p':
		case 'q':
		case 'r':
		case 's':
		case 't':
		case 'u':
		case 'v':
		case 'w':
		case 'x':
		case 'y':
		case 'z':
		case '{':
		case '|':
		case '}':
		case '~':
			flag = false;
			break;
		}
		if (flag)
		{
			return false;
		}
		nint keyboardLayout = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), out var _));
		short num = VkKeyScanEx(char.ToLowerInvariant(c), keyboardLayout);
		if (!SupportedModifiers(num))
		{
			return false;
		}
		shift = (num & 0x100) != 0;
		uint num2 = MapVirtualKeyEx((uint)(num & 0xFF), 0u, keyboardLayout);
		if (num2 == 0 || num2 > 255)
		{
			return false;
		}
		scan = (ushort)num2;
		return true;
	}

	public static bool Release(bool force = false)
	{
		if ((held == 0 && !heldShift) || (!force && Environment.TickCount64 < releaseAt))
		{
			return true;
		}
		if (releaseFailures >= 3 && !force)
		{
			return false;
		}
		List<Input> list = new List<Input>();
		if (held != 0)
		{
			list.Add(Make(held, up: true));
		}
		if (heldShift)
		{
			list.Add(Make(42, up: true));
		}
		if (SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf<Input>()) == list.Count)
		{
			held = 0;
			heldShift = false;
			releaseFailures = 0;
			return true;
		}
		releaseFailures++;
		return false;
	}

	public static bool Send(char c)
	{
		if (!ForegroundGame || Modifier || Escape || !TryMap(c, out var scan, out var shift))
		{
			return false;
		}
		if (held != 0 || heldShift)
		{
			return false;
		}
		Input[] array = ((!shift) ? new Input[1] { Make(scan, up: false) } : new Input[2]
		{
			Make(42, up: false),
			Make(scan, up: false)
		});
		uint num = SendInput((uint)array.Length, array, Marshal.SizeOf<Input>());
		if (num != array.Length)
		{
			if (shift && num != 0)
			{
				heldShift = true;
				Release(force: true);
			}
			return false;
		}
		held = scan;
		heldShift = shift;
		releaseAt = Environment.TickCount64 + 10;
		releaseFailures = 0;
		return true;
	}

	private static Input Make(ushort scan, bool up)
	{
		return new Input
		{
			Type = 1u,
			Data = new InputUnion
			{
				Keyboard = new KeyboardInput
				{
					Scan = scan,
					Flags = (uint)(8 | (up ? 2 : 0))
				}
			}
		};
	}
}
