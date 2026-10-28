using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InteropGenerator.Runtime;

namespace TataruBrawler;

internal sealed class GameReader(IGameGui gui)
{
	private readonly Dictionary<string, uint> sources = new Dictionary<string, uint>(StringComparer.Ordinal);

	private readonly FocusAcquisition focus = new FocusAcquisition();

	private readonly SeenPrompts seen = new SeenPrompts();

	private readonly TargetMotion motion = new TargetMotion();

	public string FocusState { get; private set; } = "Idle";

	public bool? FocusRequestAccepted { get; private set; }

	public int FocusAttempts => focus.Attempts;

	public bool InputVisible { get; private set; }

	public bool InputFocused { get; private set; }

	public bool InputEnabled { get; private set; }

	public bool Available { get; private set; }

	public bool ContentComplete { get; private set; }

	public IReadOnlyList<string> Words { get; private set; } = Array.Empty<string>();

	public IReadOnlyList<KtgWord> Enemies { get; private set; } = Array.Empty<KtgWord>();

	public bool FocusFailed
	{
		get
		{
			string focusState = FocusState;
			if (focusState == "Exhausted" || focusState == "MissingCollision")
			{
				return true;
			}
			return false;
		}
	}

	public void ResetFocus()
	{
		focus.Reset();
		FocusState = "Idle";
		FocusRequestAccepted = null;
	}

	public void Clear()
	{
		bool flag = (ContentComplete = false);
		bool flag3 = (Available = flag);
		bool flag5 = (InputEnabled = flag3);
		bool inputVisible = (InputFocused = flag5);
		InputVisible = inputVisible;
		Words = Array.Empty<string>();
		Enemies = Array.Empty<KtgWord>();
		sources.Clear();
		seen.Clear();
		motion.Clear();
		focus.Reset();
		FocusState = "Idle";
		FocusRequestAccepted = null;
	}

	private unsafe AtkComponentTextInput* Input(out AtkUnitBase* addon)
	{
		addon = gui.GetAddonByName<AtkUnitBase>("KTGInput");
		if (addon == null || !addon->IsVisible || !Visible(addon->RootNode))
		{
			return null;
		}
		AtkComponentBase* componentByNodeId = addon->GetComponentByNodeId(7u);
		if (componentByNodeId == null || componentByNodeId->GetComponentType() != ComponentType.TextInput)
		{
			return null;
		}
		return (AtkComponentTextInput*)componentByNodeId;
	}

	public unsafe bool FocusedNow()
	{
		AtkComponentTextInput* ptr = Input(out var _);
		if (ptr != null && ptr->Enabled)
		{
			return ptr->IsActive;
		}
		return false;
	}

	public unsafe bool CompetingFocus()
	{
		AtkComponentTextInput* ptr = Input(out var _);
		AtkStage* ptr2 = AtkStage.Instance();
		if (ptr2 == null || ptr2->AtkInputManager == null || ptr2->RaptureAtkUnitManager == null)
		{
			return true;
		}
		if (ptr2->AtkInputManager->IsTextInputActive)
		{
			if (ptr != null)
			{
				return !ptr->IsActive;
			}
			return true;
		}
		return false;
	}

	public unsafe bool EnsureFocus(long now)
	{
		AtkComponentTextInput* input = Input(out var addon);
		if (input == null || !input->Enabled)
		{
			FocusState = "InputUnavailable";
			return false;
		}
		if (CompetingFocus())
		{
			FocusState = "OtherTextInput";
			return false;
		}
		if (input->CollisionNode == null)
		{
			FocusState = "MissingCollision";
			return false;
		}
		FocusResult focusResult = focus.Step(now, input->IsActive, () =>
		{
			addon->Focus();
			FocusRequestAccepted = addon->SetFocusNode((AtkResNode*)input->CollisionNode);
		});
		FocusState = focusResult.ToString();
		return focusResult == FocusResult.Ready;
	}

	public unsafe string? EvaluatedText()
	{
		AtkComponentTextInput* ptr = Input(out var _);
		if (ptr == null)
		{
			return null;
		}
		ReadOnlySpan<byte> data = ptr->EvaluatedString.AsSpan();
		if (data.Length > 1024)
		{
			return null;
		}
		return SeString.Parse(data).TextValue;
	}

	public unsafe bool SendBackground(char key, string expected)
	{
		AtkComponentTextInput* ptr = Input(out var addon);
		if (ptr == null || !ptr->Enabled || ptr->OwnerAddon != addon || CompetingFocus())
		{
			return false;
		}
		string text = EvaluatedText();
		if (text == null || !BackgroundText.TryNext(text, key, expected, out string next))
		{
			return false;
		}
		if (key == '\r')
		{
			if (ptr->Callback == (delegate* unmanaged<AtkUnitBase*, InputCallbackType, CStringPointer, CStringPointer, int, InputCallbackResult>)null)
			{
				return false;
			}
			if (ptr->Callback(addon, InputCallbackType.Enter, ptr->RawString.StringPtr, ptr->EvaluatedString.StringPtr, ptr->CallbackEventKind) == InputCallbackResult.ClearText)
			{
				AtkComponentTextInput* ptr2 = Input(out var addon2);
				if (ptr2 == ptr && addon2 == addon)
				{
					ptr2->SetText("");
				}
			}
			return true;
		}
		ptr->SetText(next);
		if (Input(out var addon3) != null && addon3 == addon)
		{
			return string.Equals(EvaluatedText(), next, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public unsafe void Refresh()
	{
		AtkUnitBase* addonByName = gui.GetAddonByName<AtkUnitBase>("KTGResult");
		ContentComplete = addonByName != null && addonByName->IsVisible && Visible(addonByName->RootNode);
		AtkComponentTextInput* ptr = Input(out var _);
		InputVisible = ptr != null;
		InputEnabled = ptr != null && ptr->Enabled;
		InputFocused = ptr != null && ptr->IsActive;
		AtkUnitBase* addonByName2 = gui.GetAddonByName<AtkUnitBase>("KTGEWord");
		Available = InputVisible && addonByName2 != null && addonByName2->IsVisible;
		if (addonByName2 == null || !addonByName2->IsVisible || !KtgArrayReader.TryRead(out IReadOnlyList<KtgWord> words))
		{
			Available = false;
			Words = Array.Empty<string>();
			Enemies = Array.Empty<KtgWord>();
			seen.Clear();
			motion.Clear();
			return;
		}
		words = (Enemies = motion.Update(Environment.TickCount64, words));
		seen.Update(words, PromptDisplayReader.Read(addonByName2, words));
		sources.Clear();
		List<string> list = new List<string>();
		foreach (KtgWord item in EncounterPolicy.Prioritize(words.Where((KtgWord x) => x.Visible && x.Word.Length > 0 && seen.Contains(x))))
		{
			sources.TryAdd(item.Word, item.EntityId);
			if (!list.Contains(item.Word))
			{
				list.Add(item.Word);
			}
		}
		Words = list;
	}

	public unsafe bool StillVisible(string word, uint entityId = 0u)
	{
		if (entityId == 0 && !sources.TryGetValue(word, out entityId))
		{
			return false;
		}
		AtkUnitBase* addonByName = gui.GetAddonByName<AtkUnitBase>("KTGEWord");
		if (addonByName != null && addonByName->IsVisible && KtgArrayReader.TryRead(out IReadOnlyList<KtgWord> words))
		{
			return words.Any((KtgWord x) => x.Visible && x.EntityId == entityId && x.Word == word);
		}
		return false;
	}

	public uint Source(string word)
	{
		return sources.GetValueOrDefault(word);
	}

	private unsafe static bool Visible(AtkResNode* node)
	{
		int num = 0;
		while (node != null && num++ < 32)
		{
			if (!node->IsVisible() || node->Color.A == 0)
			{
				return false;
			}
			node = node->ParentNode;
		}
		return num < 32;
	}
}
