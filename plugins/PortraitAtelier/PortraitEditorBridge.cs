using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace PortraitAtelier;

/// <summary>
/// Narrow, user-invoked bridge to the portrait editor. This never reads or writes game state
/// unless the user presses Capture or Apply while the portrait editor is open.
/// </summary>
internal sealed unsafe class PortraitEditorBridge
{
    private readonly ICondition condition;
    private readonly IGameGui gameGui;
    private readonly IDataManager dataManager;

    public PortraitEditorBridge(ICondition condition, IGameGui gameGui, IDataManager dataManager)
    {
        this.condition = condition;
        this.gameGui = gameGui;
        this.dataManager = dataManager;
    }

    public PortraitPreset StyleOptions(PortraitPreset preset, PortraitStyle style, int variation, bool character, bool design)
    {
        if (!condition[ConditionFlag.EditingPortrait]) return preset;
        var editor = AgentBannerEditor.Instance();
        if (editor == null || editor->EditorState == null) return preset;
        var state = editor->EditorState;
        if (character)
            preset = PortraitCharacterStyling.Apply(preset, style, variation,
                ReadOptions(&state->Poses, true), ReadOptions(&state->Expressions, false));
        if (design)
            preset = PortraitCharacterStyling.ApplyDesign(preset, style, variation,
                ReadDesignOptions(&state->Backgrounds), ReadDesignOptions(&state->Frames), ReadDesignOptions(&state->Accents));
        return preset;
    }

    private List<PortraitCharacterOption> ReadOptions(AgentBannerEditorState.Dataset* dataset, bool pose)
    {
        var options = new List<PortraitCharacterOption>();
        if (dataset->UnlockedEntries == null || dataset->UnlockedEntriesCount > 4096) return options;
        for (var i = 0; i < dataset->UnlockedEntriesCount; i++)
        {
            var entry = dataset->UnlockedEntries[i];
            if (entry == null || entry->Row == 0 || entry->BannerConditionUnlockState != 0
                || (pose && !entry->ClassJobMatches)) continue;
            var name = pose
                ? dataManager.GetExcelSheet<Lumina.Excel.Sheets.BannerTimeline>().GetRow(entry->RowId).Name.ToString()
                : dataManager.GetExcelSheet<Lumina.Excel.Sheets.BannerFacial>().GetRow(entry->RowId).Emote.Value.Name.ToString();
            options.Add(new PortraitCharacterOption(entry->RowId, name));
        }
        return options;
    }

    private static List<PortraitCharacterOption> ReadDesignOptions(AgentBannerEditorState.Dataset* dataset)
    {
        var options = new List<PortraitCharacterOption>();
        if (dataset->UnlockedEntries == null || dataset->UnlockedEntriesCount > 4096) return options;
        for (var i = 0; i < dataset->UnlockedEntriesCount; i++)
        {
            var entry = dataset->UnlockedEntries[i];
            if (entry != null && entry->Row != 0 && entry->BannerConditionUnlockState == 0)
                options.Add(new PortraitCharacterOption(entry->RowId, ""));
        }
        return options;
    }

    public bool TryCapture(out PortraitPreset? preset, out string error)
    {
        preset = null;
        error = "";
        if (!condition[ConditionFlag.EditingPortrait])
        {
            error = "Open the in-game Portrait Editor before capturing a portrait.";
            return false;
        }

        var editor = AgentBannerEditor.Instance();
        if (editor == null || editor->EditorState == null || editor->EditorState->CharaView == null
            || !editor->EditorState->CharaView->CharaViewPortraitCharacterLoaded
            || editor->EditorState->FrameCountdown > 0)
        {
            error = "The portrait editor is not ready yet. Wait for the character preview to load and try again.";
            return false;
        }

        var state = editor->EditorState;
        var data = stackalloc ExportedPortraitData[1];
        state->CharaView->ExportPortraitData(data);
        preset = new PortraitPreset
        {
            CameraPosition = new Half4(data->CameraPosition.X, data->CameraPosition.Y, data->CameraPosition.Z, data->CameraPosition.W),
            CameraTarget = new Half4(data->CameraTarget.X, data->CameraTarget.Y, data->CameraTarget.Z, data->CameraTarget.W),
            ImageRotation = data->ImageRotation,
            CameraZoom = data->CameraZoom,
            BannerTimeline = data->BannerTimeline,
            AnimationProgress = data->AnimationProgress,
            Expression = data->Expression,
            HeadDirection = new Half2(data->HeadDirection.X, data->HeadDirection.Y),
            EyeDirection = new Half2(data->EyeDirection.X, data->EyeDirection.Y),
            DirectionalRed = data->DirectionalLightingColorRed,
            DirectionalGreen = data->DirectionalLightingColorGreen,
            DirectionalBlue = data->DirectionalLightingColorBlue,
            DirectionalBrightness = data->DirectionalLightingBrightness,
            DirectionalVerticalAngle = data->DirectionalLightingVerticalAngle,
            DirectionalHorizontalAngle = data->DirectionalLightingHorizontalAngle,
            AmbientRed = data->AmbientLightingColorRed,
            AmbientGreen = data->AmbientLightingColorGreen,
            AmbientBlue = data->AmbientLightingColorBlue,
            AmbientBrightness = data->AmbientLightingBrightness,
            BackgroundId = data->BannerBg,
            FrameId = state->BannerEntry.BannerFrame,
            DecorationId = state->BannerEntry.BannerDecoration,
        };

        if (!preset.TryValidate(out error))
        {
            preset = null;
            return false;
        }
        return true;
    }

    public bool TryApply(PortraitPreset preset, out string error, bool reframePose = false)
    {
        error = "";
        if (!preset.TryValidate(out error))
            return false;
        if (!condition[ConditionFlag.EditingPortrait])
        {
            error = "Open the in-game Portrait Editor before applying a look.";
            return false;
        }

        var editor = AgentBannerEditor.Instance();
        if (editor == null || editor->EditorState == null || editor->EditorState->CharaView == null
            || !editor->EditorState->CharaView->CharaViewPortraitCharacterLoaded
            || editor->EditorState->FrameCountdown > 0)
        {
            error = "The portrait editor is not ready yet. Wait for the character preview to load and try again.";
            return false;
        }

        var state = editor->EditorState;
        var previousChanged = state->HasDataChanged;
        if ((preset.BannerTimeline != state->BannerEntry.BannerTimeline && !IsPoseAvailable(&state->Poses, preset.BannerTimeline))
            || (preset.Expression != state->BannerEntry.Expression && FindIndex(&state->Expressions, preset.Expression) < 0)
            || (preset.BackgroundId != state->BannerEntry.BannerBg && FindIndex(&state->Backgrounds, preset.BackgroundId) < 0))
        {
            error = "This preset uses a pose, expression or background unavailable to your current character/job. Generate a new look from the open editor.";
            return false;
        }
        var previousFrame = state->BannerEntry.BannerFrame;
        var previousDecoration = state->BannerEntry.BannerDecoration;
        var frameApplied = preset.FrameId == previousFrame || state->SetFrame(preset.FrameId);
        var decorationApplied = frameApplied
            && (preset.DecorationId == previousDecoration || state->SetAccent(preset.DecorationId));
        if (!frameApplied || !decorationApplied)
        {
            if (preset.FrameId != previousFrame)
                state->SetFrame(previousFrame);
            if (preset.DecorationId != previousDecoration)
                state->SetAccent(previousDecoration);
            error = "The preset's frame or decoration was unavailable; the editor's current frame and decoration were kept.";
        }

        var data = stackalloc ExportedPortraitData[1];
        state->CharaView->ExportPortraitData(data);
        var original = *data;
        SynchronizeDropdowns(state, preset);
        state->BannerEntry.BannerBg = preset.BackgroundId;
        state->BannerEntry.BannerTimeline = preset.BannerTimeline;
        state->BannerEntry.Expression = preset.Expression;

        Set(ref data->CameraPosition, preset.CameraPosition);
        Set(ref data->CameraTarget, preset.CameraTarget);
        data->ImageRotation = preset.ImageRotation;
        data->CameraZoom = preset.CameraZoom;
        data->BannerTimeline = preset.BannerTimeline;
        data->AnimationProgress = preset.AnimationProgress;
        data->Expression = preset.Expression;
        Set(ref data->HeadDirection, preset.HeadDirection);
        Set(ref data->EyeDirection, preset.EyeDirection);
        data->DirectionalLightingColorRed = preset.DirectionalRed;
        data->DirectionalLightingColorGreen = preset.DirectionalGreen;
        data->DirectionalLightingColorBlue = preset.DirectionalBlue;
        data->DirectionalLightingBrightness = preset.DirectionalBrightness;
        data->DirectionalLightingVerticalAngle = preset.DirectionalVerticalAngle;
        data->DirectionalLightingHorizontalAngle = preset.DirectionalHorizontalAngle;
        data->AmbientLightingColorRed = preset.AmbientRed;
        data->AmbientLightingColorGreen = preset.AmbientGreen;
        data->AmbientLightingColorBlue = preset.AmbientBlue;
        data->AmbientLightingBrightness = preset.AmbientBrightness;
        data->BannerBg = preset.BackgroundId;

        // The selected frame and decoration belong to the editor's BannerEntry, separate from
        // ExportedPortraitData. The style generator preserves both from the captured base.
        state->CharaView->ImportPortraitData(data);
        if (reframePose && preset.BannerTimeline != original.BannerTimeline)
            state->CharaView->ResetCamera();
        var editorAddon = (AddonBannerEditor*)gameGui.GetAddonByName("BannerEditor").Address;
        if (editorAddon != null && preset.BannerTimeline != original.BannerTimeline && editorAddon->PlayAnimationCheckbox != null)
            editorAddon->PlayAnimationCheckbox->AtkComponentButton.IsChecked = false;
        state->CharaView->ApplyCameraPositions();
        // Native framing validity is recomputed after the preview renders. Calling
        // GetPortraitError synchronously here reports CharacterNotInFrame even for
        // a restored valid portrait. The game's Save button remains authoritative.

        var applied = stackalloc ExportedPortraitData[1];
        state->CharaView->ExportPortraitData(applied);
        if (applied->DirectionalLightingColorRed != preset.DirectionalRed
            || applied->DirectionalLightingColorGreen != preset.DirectionalGreen
            || applied->DirectionalLightingColorBlue != preset.DirectionalBlue
            || applied->DirectionalLightingBrightness != preset.DirectionalBrightness
            || applied->AmbientLightingColorRed != preset.AmbientRed
            || applied->AmbientLightingColorGreen != preset.AmbientGreen
            || applied->AmbientLightingColorBlue != preset.AmbientBlue
            || applied->AmbientLightingBrightness != preset.AmbientBrightness
            || applied->BannerTimeline != preset.BannerTimeline || applied->Expression != preset.Expression
            || applied->BannerBg != preset.BackgroundId)
        {
            RestoreOriginal(state, &original, previousFrame, previousDecoration, previousChanged);
            error = "The portrait editor did not accept all requested settings. The original preview was restored. Wait for the preview to finish loading and try again.";
            return false;
        }
        state->SetHasChanged(true);
        SynchronizeControls(applied);
        return true;
    }

    private void SynchronizeControls(ExportedPortraitData* data)
    {
        var addon = (AddonBannerEditor*)gameGui.GetAddonByName("BannerEditor").Address;
        if (addon == null)
            return;
        SetSlider(addon->AmbientLightingBrightnessSlider, data->AmbientLightingBrightness);
        SetSlider(addon->AmbientLightingColorRedSlider, data->AmbientLightingColorRed);
        SetSlider(addon->AmbientLightingColorGreenSlider, data->AmbientLightingColorGreen);
        SetSlider(addon->AmbientLightingColorBlueSlider, data->AmbientLightingColorBlue);
        SetSlider(addon->DirectionalLightingBrightnessSlider, data->DirectionalLightingBrightness);
        SetSlider(addon->DirectionalLightingColorRedSlider, data->DirectionalLightingColorRed);
        SetSlider(addon->DirectionalLightingColorGreenSlider, data->DirectionalLightingColorGreen);
        SetSlider(addon->DirectionalLightingColorBlueSlider, data->DirectionalLightingColorBlue);
        SetSlider(addon->DirectionalLightingVerticalAngleSlider, data->DirectionalLightingVerticalAngle);
        SetSlider(addon->DirectionalLightingHorizontalAngleSlider, data->DirectionalLightingHorizontalAngle);
        SetSlider(addon->CameraZoomSlider, data->CameraZoom);
        SetSlider(addon->ImageRotation, data->ImageRotation);
    }

    private void SynchronizeDropdowns(AgentBannerEditorState* state, PortraitPreset preset)
    {
        var addon = (AddonBannerEditor*)gameGui.GetAddonByName("BannerEditor").Address;
        if (addon == null) return;
        SelectDropdown(addon, 1, FindIndex(&state->Backgrounds, preset.BackgroundId));
        SelectDropdown(addon, 2, FindIndex(&state->Frames, state->BannerEntry.BannerFrame));
        SelectDropdown(addon, 3, FindIndex(&state->Accents, state->BannerEntry.BannerDecoration));
        SelectDropdown(addon, 4, FindIndex(&state->Poses, preset.BannerTimeline));
        SelectDropdown(addon, 5, FindIndex(&state->Expressions, preset.Expression, true));
        var presetIndex = state->GetPresetIndex(preset.BackgroundId, state->BannerEntry.BannerFrame, state->BannerEntry.BannerDecoration);
        if (presetIndex < 0 && addon->NumPresets > 0 && addon->Dropdowns[0].Dropdown != null)
        {
            presetIndex = addon->NumPresets - 1;
            if (addon->Dropdowns[0].Dropdown->List != null)
                addon->Dropdowns[0].Dropdown->List->SetItemCount(addon->NumPresets);
        }
        SelectDropdown(addon, 0, presetIndex);
    }

    private static void SelectDropdown(AddonBannerEditor* addon, int index, int item)
    {
        if (item >= 0 && addon->Dropdowns[index].Dropdown != null)
            addon->Dropdowns[index].Dropdown->SelectItem(item);
    }

    private static int FindIndex(AgentBannerEditorState.Dataset* dataset, ushort id, bool sorted = false)
    {
        var entries = sorted ? dataset->SortedEntries : dataset->UnlockedEntries;
        var count = sorted ? dataset->SortedEntriesCount : dataset->UnlockedEntriesCount;
        if (entries == null || count > 4096) return -1;
        for (var i = 0; i < count; i++)
            if (entries[i] != null && entries[i]->Row != 0 && entries[i]->RowId == id) return i;
        return -1;
    }

    private static bool IsPoseAvailable(AgentBannerEditorState.Dataset* dataset, ushort id)
    {
        var index = FindIndex(dataset, id);
        return index >= 0 && dataset->UnlockedEntries[index]->ClassJobMatches
            && dataset->UnlockedEntries[index]->BannerConditionUnlockState == 0;
    }

    private static void SetSlider(AtkComponentSlider* slider, int value)
    {
        if (slider != null)
            slider->SetValue(value);
    }

    private static void RestoreOriginal(AgentBannerEditorState* state, ExportedPortraitData* original,
        ushort frame, ushort decoration, bool hasChanged)
    {
        state->CharaView->ImportPortraitData(original);
        state->CharaView->ApplyCameraPositions();
        state->SetFrame(frame);
        state->SetAccent(decoration);
        state->BannerEntry.BannerBg = original->BannerBg;
        state->BannerEntry.BannerTimeline = original->BannerTimeline;
        state->BannerEntry.Expression = original->Expression;
        state->SetHasChanged(hasChanged);
    }

    private static void Set(ref FFXIVClientStructs.FFXIV.Common.Math.HalfVector4 target, Half4 value)
    {
        target.X = value.X;
        target.Y = value.Y;
        target.Z = value.Z;
        target.W = value.W;
    }

    private static void Set(ref FFXIVClientStructs.FFXIV.Common.Math.HalfVector2 target, Half2 value)
    {
        target.X = value.X;
        target.Y = value.Y;
    }
}
