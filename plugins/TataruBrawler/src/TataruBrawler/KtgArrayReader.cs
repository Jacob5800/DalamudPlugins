using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TataruBrawler;

internal static class KtgArrayReader
{
	public const int Slots = 20;

	public unsafe static bool TryRead(out IReadOnlyList<KtgWord> words)
	{
		words = Array.Empty<KtgWord>();
		AtkStage* ptr = AtkStage.Instance();
		if (ptr == null || ptr->AtkArrayDataHolder == null)
		{
			return false;
		}
		AtkArrayDataHolder* atkArrayDataHolder = ptr->AtkArrayDataHolder;
		if (atkArrayDataHolder->NumberArrayCount <= 161 || atkArrayDataHolder->StringArrayCount <= 128 || atkArrayDataHolder->NumberArrays == null || atkArrayDataHolder->StringArrays == null)
		{
			return false;
		}
		NumberArrayData* ptr2 = atkArrayDataHolder->NumberArrays[161];
		StringArrayData* ptr3 = atkArrayDataHolder->StringArrays[128];
		if (ptr2 == null || ptr3 == null || ptr2->IntArray == null || ptr3->StringArray == null || ptr2->Size < 261 || ptr2->Size > 1024 || ptr3->Size < 60 || ptr3->Size > 256 || *ptr2->IntArray < 0 || *ptr2->IntArray > 20)
		{
			return false;
		}
		List<KtgWord> list = new List<KtgWord>();
		for (int i = 0; i < *ptr2->IntArray; i++)
		{
			uint num = (uint)ptr2->IntArray[1 + 13 * i];
			if ((num != 0 && num != 3758096384u) || 1 == 0)
			{
				string text = Read(ptr3->StringArray[3 * i + 1]) ?? Read(ptr3->StringArray[3 * i]);
				list.Add(new KtgWord(num, text ?? "", i, ReadText(ptr3->StringArray[3 * i + 2]), ptr2->IntArray[1 + 13 * i + 7], ptr2->IntArray[1 + 13 * i + 11], ptr2->IntArray[1 + 13 * i + 10], ptr2->IntArray[1 + 13 * i + 4] != 0, ptr2->IntArray[1 + 13 * i + 1], ptr2->IntArray[1 + 13 * i + 2]));
			}
		}
		words = list;
		return true;
	}

	private unsafe static string ReadText(byte* ptr)
	{
		if (ptr == null)
		{
			return "";
		}
		int i;
		for (i = 0; i < 512 && ptr[i] != 0; i++)
		{
		}
		if (i != 512)
		{
			return SeString.Parse(new ReadOnlySpan<byte>(ptr, i)).TextValue.Trim();
		}
		return "";
	}

	private unsafe static string? Read(byte* ptr)
	{
		if (ptr == null)
		{
			return null;
		}
		int i;
		for (i = 0; i < 512 && ptr[i] != 0; i++)
		{
		}
		if (i == 512)
		{
			return null;
		}
		string text = SeString.Parse(new ReadOnlySpan<byte>(ptr, i)).TextValue.Trim();
		if (!(TypingPlan.Create(text) != null))
		{
			return null;
		}
		return text;
	}
}
