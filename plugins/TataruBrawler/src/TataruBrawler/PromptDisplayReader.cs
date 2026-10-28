using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TataruBrawler;

internal static class PromptDisplayReader
{
	[StructLayout(LayoutKind.Explicit, Size = 200)]
	private struct Entry
	{
		[FieldOffset(0)]
		public unsafe AtkComponentNode* Root;

		[FieldOffset(168)]
		public uint EntityId;
	}

	public unsafe static IReadOnlyList<PromptDisplay> Read(AtkUnitBase* addon, IReadOnlyList<KtgWord> current)
	{
		List<PromptDisplay> list = new List<PromptDisplay>();
		if (addon == null || !addon->IsVisible || !Visible(addon->RootNode))
		{
			return list;
		}
		AtkUldManager* ptr = &addon->UldManager;
		if (ptr->NodeList == null || ptr->NodeListCount > 512)
		{
			return list;
		}
		Entry* ptr2 = (Entry*)((byte*)addon + 568);
		for (int i = 0; i < 20; i++)
		{
			Entry entry = ptr2[i];
			KtgWord ktgWord = current.FirstOrDefault((KtgWord x) => x.EntityId == entry.EntityId && x.Visible && x.Word.Length > 0);
			if (ktgWord == null || entry.Root == null)
			{
				continue;
			}
			bool flag = false;
			for (int num = 0; num < ptr->NodeListCount; num++)
			{
				if (ptr->NodeList[num] == entry.Root && (int)ptr->NodeList[num]->Type >= 1000)
				{
					flag = true;
					break;
				}
			}
			if (flag && entry.Root->Component != null)
			{
				AtkComponentBase* component = entry.Root->Component;
				AtkTextNode* textNodeById = component->GetTextNodeById(20u);
				AtkTextNode* textNodeById2 = component->GetTextNodeById(22u);
				string text = Text(textNodeById2);
				bool textVisible = (text == ktgWord.Word || (text.Length == 0 && Text(textNodeById) == ktgWord.Word)) && Visible((AtkResNode*)entry.Root) && (Visible((AtkResNode*)textNodeById) || Visible((AtkResNode*)textNodeById2));
				list.Add(new PromptDisplay(entry.EntityId, ktgWord.Word, textVisible));
			}
		}
		return list;
	}

	private unsafe static string Text(AtkTextNode* node)
	{
		if (node == null)
		{
			return "";
		}
		ReadOnlySpan<byte> data = node->NodeText.AsSpan();
		if (data.Length > 512)
		{
			return "";
		}
		return SeString.Parse(data).TextValue.Trim();
	}

	private unsafe static bool Visible(AtkResNode* node)
	{
		if (node == null)
		{
			return false;
		}
		int num = 0;
		while (node != null && num++ < 32)
		{
			if (!node->IsVisible() || node->Color.A == 0 || node->ScaleX == 0f || node->ScaleY == 0f)
			{
				return false;
			}
			node = node->ParentNode;
		}
		return num < 32;
	}
}
