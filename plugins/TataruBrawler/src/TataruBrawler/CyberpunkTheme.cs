using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace TataruBrawler;

internal static class CyberpunkTheme
{
	private const int ColorCount = 25;

	private const int VarCount = 4;

	public static void Push()
	{
		ImGui.PushStyleColor(ImGuiCol.WindowBg, Rgb(10, 12, 27));
		ImGui.PushStyleColor(ImGuiCol.ChildBg, Rgb(14, 18, 35));
		ImGui.PushStyleColor(ImGuiCol.PopupBg, Rgb(18, 20, 42));
		ImGui.PushStyleColor(ImGuiCol.Text, Rgb(229, 246, 250));
		ImGui.PushStyleColor(ImGuiCol.TextDisabled, Rgb(126, 151, 176));
		ImGui.PushStyleColor(ImGuiCol.Border, Rgb(39, 207, 225));
		ImGui.PushStyleColor(ImGuiCol.Separator, Rgb(38, 145, 179));
		ImGui.PushStyleColor(ImGuiCol.FrameBg, Rgb(24, 32, 57));
		ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Rgb(36, 52, 81));
		ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Rgb(42, 61, 93));
		ImGui.PushStyleColor(ImGuiCol.TitleBg, Rgb(18, 17, 43));
		ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Rgb(35, 20, 65));
		ImGui.PushStyleColor(ImGuiCol.CheckMark, Rgb(52, 239, 229));
		ImGui.PushStyleColor(ImGuiCol.Button, Rgb(66, 34, 104));
		ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Rgb(103, 44, 146));
		ImGui.PushStyleColor(ImGuiCol.ButtonActive, Rgb(141, 53, 176));
		ImGui.PushStyleColor(ImGuiCol.Header, Rgb(52, 37, 89));
		ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Rgb(69, 51, 115));
		ImGui.PushStyleColor(ImGuiCol.HeaderActive, Rgb(92, 58, 136));
		ImGui.PushStyleColor(ImGuiCol.Tab, Rgb(26, 27, 55));
		ImGui.PushStyleColor(ImGuiCol.TabHovered, Rgb(103, 44, 146));
		ImGui.PushStyleColor(ImGuiCol.TabActive, Rgb(62, 36, 102));
		ImGui.PushStyleColor(ImGuiCol.SliderGrab, Rgb(44, 210, 221));
		ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Rgb(239, 68, 177));
		ImGui.PushStyleColor(ImGuiCol.ResizeGrip, Rgb(44, 210, 221));
		ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 7f);
		ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
		ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 4f);
		ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 4f);
	}

	public static void Pop()
	{
		ImGui.PopStyleVar(4);
		ImGui.PopStyleColor(25);
	}

	private static Vector4 Rgb(byte r, byte g, byte b)
	{
		return new Vector4((float)(int)r / 255f, (float)(int)g / 255f, (float)(int)b / 255f, 1f);
	}
}
