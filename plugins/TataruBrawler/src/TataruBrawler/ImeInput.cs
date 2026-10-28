using System.Runtime.InteropServices;

namespace TataruBrawler;

internal sealed class ImeInput
{
	private nint window;

	private nint savedContext;

	private bool originalOpen;

	private bool owned;

	private uint originalConversion;

	private uint originalSentence;

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("imm32.dll")]
	private static extern nint ImmGetContext(nint hwnd);

	[DllImport("imm32.dll")]
	private static extern bool ImmReleaseContext(nint hwnd, nint context);

	[DllImport("imm32.dll")]
	private static extern bool ImmGetOpenStatus(nint context);

	[DllImport("imm32.dll")]
	private static extern bool ImmSetOpenStatus(nint context, bool open);

	[DllImport("imm32.dll")]
	private static extern bool ImmGetConversionStatus(nint context, out uint conversion, out uint sentence);

	[DllImport("imm32.dll")]
	private static extern bool ImmSetConversionStatus(nint context, uint conversion, uint sentence);

	[DllImport("imm32.dll", EntryPoint = "ImmGetCompositionStringW")]
	private static extern int CompositionLength(nint context, uint index, nint buffer, uint size);

	public ImeState Inspect()
	{
		if (!Keyboard.ForegroundGame)
		{
			return default;
		}
		nint foregroundWindow = GetForegroundWindow();
		nint num = ImmGetContext(foregroundWindow);
		if (num == 0)
		{
			return default;
		}
		try
		{
			bool flag = ImmGetConversionStatus(num, out var conversion, out var _);
			int num2 = CompositionLength(num, 8u, 0, 0u);
			return new ImeState(flag && num2 >= -1, ImmGetOpenStatus(num), conversion, num2 > 0);
		}
		finally
		{
			ImmReleaseContext(foregroundWindow, num);
		}
	}

	public bool Prepare(bool japanese)
	{
		if (!Keyboard.ForegroundGame)
		{
			return false;
		}
		nint foregroundWindow = GetForegroundWindow();
		nint num = ImmGetContext(foregroundWindow);
		if (num == 0)
		{
			return !japanese;
		}
		try
		{
			int num2 = CompositionLength(num, 8u, 0, 0u);
			if (!ImmGetConversionStatus(num, out var conversion, out var sentence) || num2 > 0 || num2 < -1)
			{
				return false;
			}
			if (!owned)
			{
				window = foregroundWindow;
				savedContext = num;
				originalOpen = ImmGetOpenStatus(num);
				originalConversion = conversion;
				originalSentence = sentence;
				owned = true;
			}
			if (window != foregroundWindow || savedContext != num)
			{
				return false;
			}
			if (ImmGetOpenStatus(num) != japanese && !ImmSetOpenStatus(num, japanese))
			{
				return false;
			}
			uint num3 = (conversion | 9) & 0xFFFFFFFDu;
			if (japanese && num3 != conversion && !ImmSetConversionStatus(num, num3, sentence))
			{
				return false;
			}
			return true;
		}
		finally
		{
			ImmReleaseContext(foregroundWindow, num);
		}
	}

	public void Restore()
	{
		if (!owned)
		{
			return;
		}
		owned = false;
		nint num = ImmGetContext(window);
		if (num == 0)
		{
			return;
		}
		try
		{
			bool flag = num == savedContext;
			if (flag)
			{
				int num2 = CompositionLength(num, 8u, 0, 0u);
				bool flag2 = (uint)(num2 - -1) <= 1u;
				flag = flag2;
			}
			if (flag)
			{
				ImmSetConversionStatus(num, originalConversion, originalSentence);
				ImmSetOpenStatus(num, originalOpen);
			}
		}
		finally
		{
			ImmReleaseContext(window, num);
			window = (savedContext = 0);
		}
	}
}
