using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TataruBrawler;

internal sealed class DiagnosticReader(IGameGui gui)
{
	private readonly DiagnosticDelta delta = new DiagnosticDelta();

	private string? unavailable;

	public void Reset()
	{
		delta.Reset();
		unavailable = null;
	}

	public unsafe string? Capture(TypingSession typing, ImeState ime, bool paused, object control)
	{
		AtkStage* ptr = AtkStage.Instance();
		if (ptr == null || ptr->AtkArrayDataHolder == null)
		{
			return Unavailable("stage_missing");
		}
		AtkArrayDataHolder* atkArrayDataHolder = ptr->AtkArrayDataHolder;
		if (atkArrayDataHolder->NumberArrayCount <= 161 || atkArrayDataHolder->StringArrayCount <= 128 || atkArrayDataHolder->NumberArrays == null || atkArrayDataHolder->StringArrays == null)
		{
			return Unavailable("array_capacity_mismatch");
		}
		NumberArrayData* ptr2 = atkArrayDataHolder->NumberArrays[161];
		StringArrayData* ptr3 = atkArrayDataHolder->StringArrays[128];
		if (ptr2 == null || ptr3 == null || ptr2->IntArray == null || ptr3->StringArray == null || ptr2->Size < 261 || ptr2->Size > 1024 || ptr3->Size < 60 || ptr3->Size > 256)
		{
			return Unavailable("array_layout_mismatch");
		}
		if (*ptr2->IntArray < 0 || *ptr2->IntArray > 20)
		{
			return Unavailable("enemy_count_invalid");
		}
		unavailable = null;
		int[] numbers = new ReadOnlySpan<int>(ptr2->IntArray, 261).ToArray();
		string[] array = new string[60];
		for (int i = 0; i < 60; i++)
		{
			array[i] = ReadText(ptr3->StringArray[i]);
		}
		AtkUnitBase* addonByName = gui.GetAddonByName<AtkUnitBase>("KTGInput");
		var value = new
		{
			visible = (addonByName != null && addonByName->IsVisible),
			enabled = false,
			focused = false,
			inputLength = 0
		};
		if (addonByName != null)
		{
			AtkComponentBase* componentByNodeId = addonByName->GetComponentByNodeId(7u);
			if (componentByNodeId != null && componentByNodeId->GetComponentType() == ComponentType.TextInput)
			{
				AtkComponentTextInput* ptr4 = (AtkComponentTextInput*)componentByNodeId;
				value = new
				{
					visible = addonByName->IsVisible,
					enabled = ptr4->Enabled,
					focused = ptr4->IsActive,
					inputLength = Math.Clamp(ptr4->RawString.Length, 0, 1024)
				};
			}
		}
		Dictionary<string, object> dictionary = new Dictionary<string, object>
		{
			["control"] = control,
			["input"] = value,
			["word"] = Probe("KTGEWord"),
			["cast"] = Probe("KTGEnemyCastBar"),
			["hp"] = Probe("KTGHudGaugeHp"),
			["typing"] = new { typing.Running, typing.ManualPaused, typing.Status, typing.Position, typing.Sent, typing.AnswerCheck, paused },
			["ime"] = ime,
			["inputHints"] = new
			{
				label = ReadNode(addonByName, 3u),
				hint = ReadNode(addonByName, 6u)
			}
		};
		if (KtgArrayReader.TryRead(out IReadOnlyList<KtgWord> words))
		{
			dictionary["promptDisplay"] = (from x in PromptDisplayReader.Read(gui.GetAddonByName<AtkUnitBase>("KTGEWord"), words)
				select new { x.EntityId, x.TextVisible }).ToArray();
		}
		return delta.Capture(numbers, array, dictionary);
	}

	private unsafe static string ReadNode(AtkUnitBase* addon, uint id)
	{
		if (addon == null)
		{
			return "";
		}
		AtkTextNode* textNodeById = addon->GetTextNodeById(id);
		if (textNodeById != null)
		{
			return ReadText(textNodeById->NodeText.StringPtr);
		}
		return "";
	}

	private string? Unavailable(string reason)
	{
		if (unavailable == reason)
		{
			return null;
		}
		unavailable = reason;
		delta.Reset();
		return JsonSerializer.Serialize(new
		{
			available = false,
			reason = reason
		});
	}

	private unsafe object Probe(string name)
	{
		AtkUnitBase* addonByName = gui.GetAddonByName<AtkUnitBase>(name);
		if (addonByName == null)
		{
			return new
			{
				exists = false
			};
		}
		List<object> list = new List<object>();
		AtkUldManager* ptr = &addonByName->UldManager;
		if (ptr->NodeList != null && ptr->NodeListCount <= 512)
		{
			for (int i = 0; i < ptr->NodeListCount; i++)
			{
				if (list.Count >= 20)
				{
					break;
				}
				AtkResNode* ptr2 = ptr->NodeList[i];
				if (ptr2 != null && (int)ptr2->Type >= 1000)
				{
					list.Add(new
					{
						ordinal = list.Count,
						nodeId = ptr2->NodeId,
						visible = ptr2->IsVisible(),
						alpha = ptr2->Color.A
					});
				}
			}
		}
		return new
		{
			exists = true,
			visible = addonByName->IsVisible,
			components = list
		};
	}

	private unsafe static string ReadText(byte* ptr)
	{
		if (ptr == null)
		{
			return "";
		}
		int i;
		for (i = 0; i < 192 && ptr[i] != 0; i++)
		{
		}
		if (i == 192)
		{
			return "[truncated]";
		}
		return SeString.Parse(new ReadOnlySpan<byte>(ptr, i)).TextValue;
	}
}
