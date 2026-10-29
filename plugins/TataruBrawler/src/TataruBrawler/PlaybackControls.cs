using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace TataruBrawler;

internal static class PlaybackControls
{
	public static void Draw(bool running, bool paused, Func<string, string> text, Action start, Action pause, Action stop, bool canPause = true)
	{
		Button("Start", 0, running && !paused, text(paused ? "Resume" : "Start"), start, running && !paused);
		ImGui.SameLine();
		Button("Pause", 1, !canPause | paused, text("Pause"), pause, paused);
		ImGui.SameLine();
		Button("Stop", 2, !running, text("Stop"), stop, !running);
	}

	public static void DrawLoop(bool enabled, string tooltip, Action action, bool active = false)
	{
		PushActive(active);
		ImGui.BeginDisabled(!enabled);
		bool flag = ImGui.Button("###LoopStart", new Vector2(36f, 30f));
		Vector2 itemRectMin = ImGui.GetItemRectMin();
		ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();
		uint colorU = ImGui.GetColorU32(ImGuiCol.Text);
		windowDrawList.AddLine(itemRectMin + new Vector2(8f, 9f), itemRectMin + new Vector2(27f, 9f), colorU, 2f);
		windowDrawList.AddLine(itemRectMin + new Vector2(27f, 9f), itemRectMin + new Vector2(27f, 16f), colorU, 2f);
		windowDrawList.AddTriangleFilled(itemRectMin + new Vector2(22f, 12f), itemRectMin + new Vector2(27f, 17f), itemRectMin + new Vector2(31f, 12f), colorU);
		windowDrawList.AddLine(itemRectMin + new Vector2(28f, 22f), itemRectMin + new Vector2(9f, 22f), colorU, 2f);
		windowDrawList.AddLine(itemRectMin + new Vector2(9f, 22f), itemRectMin + new Vector2(9f, 15f), colorU, 2f);
		windowDrawList.AddTriangleFilled(itemRectMin + new Vector2(4f, 18f), itemRectMin + new Vector2(9f, 13f), itemRectMin + new Vector2(14f, 18f), colorU);
		ImGui.EndDisabled();
        PopActive(active);
		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.SetTooltip(tooltip);
		}
		if (flag)
		{
			action();
		}
	}

    private static void PushActive(bool active)
    {
        if (!active) return;
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.08f, .40f, .43f, 1));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.25f, .85f, .86f, 1));
        ImGui.PushStyleVar(ImGuiStyleVar.DisabledAlpha, 1f);
    }
    private static void PopActive(bool active)
    {
        if (!active) return;
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);
    }

	private static void Button(string id, int symbol, bool disabled, string tooltip, Action action, bool active = false)
	{
		PushActive(active);
        ImGui.BeginDisabled(disabled);
		bool flag = ImGui.Button("###Playback" + id, new Vector2(36f, 30f));
		Vector2 vector = ImGui.GetItemRectMin() + new Vector2(10f, 8f);
		ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();
		uint colorU = ImGui.GetColorU32(ImGuiCol.Text);
		switch (symbol)
		{
		case 0:
			windowDrawList.AddTriangleFilled(vector, vector + new Vector2(0f, 14f), vector + new Vector2(14f, 7f), colorU);
			break;
		case 1:
			windowDrawList.AddRectFilled(vector, vector + new Vector2(5f, 14f), colorU);
			windowDrawList.AddRectFilled(vector + new Vector2(9f, 0f), vector + new Vector2(14f, 14f), colorU);
			break;
		default:
			windowDrawList.AddRectFilled(vector, vector + new Vector2(14f, 14f), colorU);
			break;
		}
		ImGui.EndDisabled();
        PopActive(active);
		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.SetTooltip(tooltip);
		}
		if (flag)
		{
			action();
		}
	}
}
