using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin.Services;

namespace TataruBrawler;

internal sealed class LocalizedFonts : IDisposable
{
	private IFontHandle[] fonts = Array.Empty<IFontHandle>();

	private IFontHandle[] pending = Array.Empty<IFontHandle>();

	private readonly IFontAtlas atlas;

	private readonly IPluginLog log;

	private readonly GlyphCoverage coverage = new GlyphCoverage();

	private string? builtText;

	private bool failed;

	public bool Available
	{
		get
		{
			if (pending.Length == 0 && fonts.Length == 4)
			{
				return fonts.All((IFontHandle x) => x.Available);
			}
			return false;
		}
	}

	public bool Failed => failed;

	public LocalizedFonts(IFontAtlas atlas, IPluginLog log, IEnumerable<string> initialText)
	{
		this.atlas = atlas;
		this.log = log;
		Ensure(initialText.Prepend(Loc.FontText + GlyphCoverage.TypingCharacters));
	}

	public void Ensure(IEnumerable<string> text)
	{
		bool flag = coverage.Include(text);
		if (failed)
		{
			return;
		}
		if (pending.Length != 0)
		{
			Exception ex = pending.Select((IFontHandle x) => x.LoadException).FirstOrDefault((Exception x) => x != null);
			IFontHandle[] array;
			if (ex != null)
			{
				failed = true;
				log.Error(ex, "Tataru Brawler UI font build failed");
				array = pending;
				for (int num = 0; num < array.Length; num++)
				{
					array[num].Dispose();
				}
				pending = Array.Empty<IFontHandle>();
				return;
			}
			if (!pending.All((IFontHandle x) => x.Available))
			{
				return;
			}
			IFontHandle[] array2 = fonts;
			fonts = pending;
			pending = Array.Empty<IFontHandle>();
			array = array2;
			for (int num = 0; num < array.Length; num++)
			{
				array[num].Dispose();
			}
		}
		if (!flag && builtText != null && coverage.Count == builtText.Length)
		{
			return;
		}
		string text2 = coverage.Text;
		if (text2 == builtText)
		{
			return;
		}
		ushort[] ranges = text2.ToGlyphRange();
		List<IFontHandle> list = new List<IFontHandle>();
		using (atlas.SuppressAutoRebuild())
		{
			try
			{
				for (int num2 = 0; num2 < 4; num2++)
				{
					int fontFace = num2;
					list.Add(atlas.NewDelegateFontHandle((IFontAtlasBuildToolkit step) =>
					{
						step.OnPreBuild((IFontAtlasBuildToolkitPreBuild tk) =>
						{
							SafeFontConfig fontConfig = new SafeFontConfig
							{
								SizePx = 17f,
								FontNo = fontFace,
								GlyphRanges = ranges
							};
							tk.Font = tk.AddDalamudAssetFont(DalamudAsset.NotoSansCjkRegular, in fontConfig);
						});
					}));
				}
			}
			catch
			{
				foreach (IFontHandle item in list)
				{
					item.Dispose();
				}
				throw;
			}
			pending = list.ToArray();
			builtText = text2;
		}
	}

	private static int Face(string? code)
	{
		return code switch
		{
			"zh-Hant" => 1, 
			"zh-Hans" => 2, 
			"ko" => 3, 
			_ => 0, 
		};
	}

	public IDisposable? Push(string? code)
	{
		if (!Available)
		{
			return null;
		}
		return fonts[Face(code)].Push();
	}

	public void Dispose()
	{
		IFontHandle[] array = pending;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Dispose();
		}
		array = fonts;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Dispose();
		}
		pending = Array.Empty<IFontHandle>();
		fonts = Array.Empty<IFontHandle>();
	}
}
