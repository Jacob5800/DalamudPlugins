using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace TataruBrawler.Ui;

internal static class StandardPluginTabs
{
	public static IReadOnlyList<(string Code, string SelfName)> Languages { get; } = new _003C_003Ez__ReadOnlyArray<(string, string)>(new (string, string)[7]
	{
		("ja", "日本語"),
		("en", "English"),
		("de", "Deutsch"),
		("fr", "Français"),
		("ko", "한국어"),
		("zh-Hans", "简体中文"),
		("zh-Hant", "繁體中文")
	});

	public static void DrawTrailingTabs(string settingsLabel, Action drawSettings, string languageLabel, Action drawLanguage)
	{
		ImU8String label = new ImU8String(11, 1);
		label.AppendFormatted(settingsLabel);
		label.AppendLiteral("###Settings");
		if (ImGui.BeginTabItem(label))
		{
			drawSettings();
			ImGui.EndTabItem();
		}
		ImU8String label2 = new ImU8String(19, 1);
		label2.AppendFormatted(languageLabel);
		label2.AppendLiteral("###LanguageSettings");
		if (ImGui.BeginTabItem(label2))
		{
			drawLanguage();
			ImGui.EndTabItem();
		}
	}

	public static string? DrawLanguageSelector(string label, string help, string currentCode)
	{
		(string, string) tuple = Languages.FirstOrDefault(((string Code, string SelfName) x) => x.Code == currentCode);
		string text = (string.IsNullOrEmpty(tuple.Item1) ? "English" : tuple.Item2);
		string result = null;
		ImGui.SetNextItemWidth(220f);
		ImU8String label2 = new ImU8String(18, 1);
		label2.AppendFormatted(label);
		label2.AppendLiteral("###DisplayLanguage");
		if (ImGui.BeginCombo(label2, text))
		{
			foreach (var language in Languages)
			{
				string item = language.Code;
				string item2 = language.SelfName;
				ImU8String label3 = new ImU8String(3, 2);
				label3.AppendFormatted(item2);
				label3.AppendLiteral("###");
				label3.AppendFormatted(item);
				if (ImGui.Selectable(label3, item == currentCode))
				{
					result = item;
				}
			}
			ImGui.EndCombo();
		}
		ImGui.TextWrapped(help);
		return result;
	}
}
