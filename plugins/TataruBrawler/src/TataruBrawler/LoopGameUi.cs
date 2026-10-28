using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace TataruBrawler;

internal sealed class LoopGameUi
{
	public const uint LobbyTerritory = 1360u;

	public const uint GameTerritory = 1359u;

	public const uint DutyId = 1095u;

	private readonly IGameGui gui;

	private readonly IObjectTable objects;

	private readonly string dutyName;

	private readonly string exitPrompt;

	public bool Supported { get; }

	public LoopGameUi(IGameGui gui, IDataManager data, IObjectTable objects)
	{
		this.gui = gui;
		this.objects = objects;
		ContentFinderCondition? rowOrDefault = data.GetExcelSheet<ContentFinderCondition>().GetRowOrDefault(1095u);
		TerritoryType? rowOrDefault2 = data.GetExcelSheet<TerritoryType>().GetRowOrDefault(1360u);
		Supported = rowOrDefault.HasValue && rowOrDefault.GetValueOrDefault().TerritoryType.RowId == 1359 && rowOrDefault2?.Name.ToString() == "w1eo";
		dutyName = rowOrDefault?.Name.ToString() ?? "";
		exitPrompt = data.GetExcelSheet<Addon>().GetRowOrDefault(17854u)?.Text.ToString() ?? "";
	}

	private unsafe static bool Visible(AtkUnitBase* addon)
	{
		if (addon != null && addon->IsVisible && addon->RootNode != null)
		{
			return addon->RootNode->IsVisible();
		}
		return false;
	}

	private unsafe static bool Ready(AtkComponentButton* button)
	{
		if (button != null && button->OwnerNode != null && button->IsEnabled)
		{
			return button->OwnerNode->IsVisible();
		}
		return false;
	}

	private unsafe static string Text(AtkTextNode* node)
	{
		if (node != null && node->NodeText.Length <= 2048)
		{
			return SeString.Parse(node->NodeText.AsSpan()).TextValue;
		}
		return "";
	}

	private static string Normalize(string text)
	{
		return string.Concat(text.Where((char c) => !char.IsWhiteSpace(c)));
	}

	private unsafe bool MatchingConfirmation(AddonContentsFinderConfirm* confirm)
	{
		if (Visible((AtkUnitBase*)confirm) && Text(confirm->AtkTextNode230).Trim() == dutyName)
		{
			return Ready(confirm->CommenceButton);
		}
		return false;
	}

	private unsafe bool MatchingExit(AddonSelectYesno* confirm)
	{
		if (Visible((AtkUnitBase*)confirm) && dutyName.Length > 0 && exitPrompt.Length > 0 && Text(confirm->PromptText).Contains(dutyName, StringComparison.Ordinal) && Normalize(Text(confirm->PromptText).Replace(dutyName, "", StringComparison.Ordinal)) == Normalize(exitPrompt))
		{
			return Ready(confirm->YesButton);
		}
		return false;
	}

	private static bool Own(ContentsId entry)
	{
		if (entry.ContentType == ContentsType.Regular)
		{
			return entry.Id == 1095;
		}
		return false;
	}

	private nint Registrar()
	{
		IPlayerCharacter localPlayer = objects.LocalPlayer;
		if (localPlayer == null)
		{
			return 0;
		}
		nint num = 0;
		foreach (IGameObject @object in objects)
		{
			if (@object.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventNpc && !(@object.Name.TextValue != "Keybound Registrar") && !(Vector3.Distance(localPlayer.Position, @object.Position) > 5f))
			{
				if (num != 0)
				{
					return 0;
				}
				num = @object.Address;
			}
		}
		return num;
	}

	private unsafe static AtkComponentButton* RegistrationButton()
	{
		AtkStage* ptr = AtkStage.Instance();
		if (ptr == null || ptr->RaptureAtkUnitManager == null)
		{
			return null;
		}
		AtkUnitBase* focusedAddon = ptr->RaptureAtkUnitManager->FocusedAddon;
		if (!Visible(focusedAddon))
		{
			return null;
		}
		AtkUldManager* ptr2 = &focusedAddon->UldManager;
		if (ptr2->NodeList == null || ptr2->NodeListCount > 512)
		{
			return null;
		}
		AtkComponentButton* ptr3 = null;
		for (int i = 0; i < ptr2->NodeListCount; i++)
		{
			AtkResNode* ptr4 = ptr2->NodeList[i];
			if (ptr4 == null || (int)ptr4->Type < 1000)
			{
				continue;
			}
			AtkComponentBase* component = ((AtkComponentNode*)ptr4)->Component;
			if (component == null || component->GetComponentType() != ComponentType.Button)
			{
				continue;
			}
			AtkComponentButton* ptr5 = (AtkComponentButton*)component;
			if (Ready(ptr5) && !(Text(ptr5->ButtonTextNode).Trim() != "Register"))
			{
				if (ptr3 != null)
				{
					return null;
				}
				ptr3 = ptr5;
			}
		}
		return ptr3;
	}

	private unsafe AtkUnitBase* RegistrationAddon()
	{
		AtkStage* ptr = AtkStage.Instance();
		AtkUnitBase* ptr2 = ((ptr == null || ptr->RaptureAtkUnitManager == null) ? null : ptr->RaptureAtkUnitManager->FocusedAddon);
		if (!Visible(ptr2) || dutyName.Length == 0)
		{
			return null;
		}
		if (!Encoding.UTF8.GetString(ptr2->Name).TrimEnd('\0').StartsWith("KTG", StringComparison.Ordinal))
		{
			AtkUldManager* ptr3 = &ptr2->UldManager;
			bool flag = false;
			if (ptr3->NodeList == null || ptr3->NodeListCount > 512)
			{
				return null;
			}
			for (int i = 0; i < ptr3->NodeListCount; i++)
			{
				AtkResNode* ptr4 = ptr3->NodeList[i];
				if (ptr4 != null)
				{
					AtkTextNode* asAtkTextNode = ptr4->GetAsAtkTextNode();
					if (asAtkTextNode != null && ptr4->IsVisible() && Text(asAtkTextNode).Contains(dutyName, StringComparison.Ordinal))
					{
						flag = true;
						break;
					}
				}
			}
			if (!flag)
			{
				return null;
			}
		}
		return ptr2;
	}

	public unsafe LoopSnapshot Read(bool loggedIn, uint territory, bool loading)
	{
		ContentsFinder* ptr = ContentsFinder.Instance();
		bool flag = false;
		bool otherQueue = false;
		if (ptr != null)
		{
			ContentsFinderQueueInfo* ptr2 = &ptr->QueueInfo;
			ContentsFinderQueueState queueState = ptr2->QueueState;
			bool flag2 = queueState - 1 <= ContentsFinderQueueState.Ready;
			flag = flag2;
			if (flag)
			{
				bool flag3 = Own(ptr2->PoppedQueueEntry);
				bool flag4 = false;
				for (int i = 0; i < 5; i++)
				{
					ContentsId entry = ptr2->QueuedEntries[i];
					if (entry.ContentType != ContentsType.None)
					{
						flag3 |= Own(entry);
						flag4 |= !Own(entry);
					}
				}
				otherQueue = flag4 || !flag3;
			}
			if (ptr2->QueueState == ContentsFinderQueueState.InContent && territory != 1359)
			{
				flag = true;
			}
		}
		AtkUnitBase* addonByName = gui.GetAddonByName<AtkUnitBase>("KTGResult");
		AtkComponentButton* button = (AtkComponentButton*)(Visible(addonByName) ? addonByName->GetComponentByNodeId(48u) : null);
		AddonContentsFinderConfirm* addonByName2 = gui.GetAddonByName<AddonContentsFinderConfirm>("ContentsFinderConfirm");
		return new LoopSnapshot(loggedIn, territory == 1360, territory == 1359, loading, otherQueue, flag, MatchingConfirmation(addonByName2), Visible(addonByName), Ready(button), MatchingExit(gui.GetAddonByName<AddonSelectYesno>("SelectYesno")), territory == 1360 && Registrar() != 0, territory == 1360 && RegistrationAddon() != null && RegistrationButton() != null);
	}

	public unsafe bool Execute(LoopAction action, uint territory)
	{
		if (!Supported)
		{
			return false;
		}
		switch (action)
		{
		case LoopAction.InteractRegistrar:
		{
			ContentsFinder* ptr = ContentsFinder.Instance();
			if (territory != 1360 || ptr == null || ptr->QueueInfo.QueueState != ContentsFinderQueueState.None)
			{
				return false;
			}
			GameObject* ptr2 = (GameObject*)Registrar();
			TargetSystem* ptr3 = TargetSystem.Instance();
			if (ptr2 == null || ptr3 == null)
			{
				return false;
			}
			ptr3->SetHardTarget(ptr2);
			ptr3->InteractWithObject(ptr2);
			return true;
		}
		case LoopAction.Register:
		{
			AtkUnitBase* ptr4 = RegistrationAddon();
			if (territory == 1360 && ptr4 != null)
			{
				return Click(ptr4, RegistrationButton());
			}
			return false;
		}
		case LoopAction.Accept:
		{
			AddonContentsFinderConfirm* addonByName2 = gui.GetAddonByName<AddonContentsFinderConfirm>("ContentsFinderConfirm");
			if (MatchingConfirmation(addonByName2))
			{
				return Click((AtkUnitBase*)addonByName2, addonByName2->CommenceButton);
			}
			return false;
		}
		case LoopAction.Leave:
		{
			AtkUnitBase* addonByName3 = gui.GetAddonByName<AtkUnitBase>("KTGResult");
			if (territory == 1359 && Visible(addonByName3))
			{
				return Click(addonByName3, (AtkComponentButton*)addonByName3->GetComponentByNodeId(48u));
			}
			return false;
		}
		case LoopAction.ConfirmExit:
		{
			AddonSelectYesno* addonByName = gui.GetAddonByName<AddonSelectYesno>("SelectYesno");
			if (territory == 1359 && MatchingExit(addonByName))
			{
				return Click((AtkUnitBase*)addonByName, addonByName->YesButton);
			}
			return false;
		}
		default:
			return false;
		}
	}

	private unsafe static bool Click(AtkUnitBase* addon, AtkComponentButton* button)
	{
		if (!Visible(addon) || !Ready(button) || button->GetComponentType() != ComponentType.Button)
		{
			return false;
		}
		AtkEvent* ptr = button->OwnerNode->AtkEventManager.Event;
		int num = 0;
		while (ptr != null && num < 64)
		{
			if (ptr->State.EventType == AtkEventType.ButtonClick && ptr->Listener == addon)
			{
				AtkEvent atkEvent = *ptr;
				atkEvent.NextEvent = null;
				atkEvent.State.ReturnFlags = 0;
				atkEvent.State.StateFlags = AtkEventStateFlags.None;
				AtkEventData atkEventData = default;
				ptr->Listener->ReceiveEvent(AtkEventType.ButtonClick, (int)ptr->Param, &atkEvent, &atkEventData);
				return true;
			}
			num++;
			ptr = ptr->NextEvent;
		}
		return false;
	}
}
