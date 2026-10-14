using System.Numerics;
using Dalamud.Bindings.ImGui;
internal static class DiscordJoinButton
{
    internal static bool Draw(bool iconOnly = false)
    {
        var height = ImGui.GetFrameHeight();
        var origin = ImGui.GetCursorScreenPos();
        var clicked = ImGui.Button("##DiscordJoin", new Vector2(iconOnly ? height : 116, height));
        var list = ImGui.GetWindowDrawList();
        var scale = (height - 8) / 18;
        var start = origin + new Vector2(4, 4);
        Vector2 P(float x, float y) => start + new Vector2(x, y) * scale;
        const uint blue = 0xFFF26558;
        const uint white = 0xFFFFFFFF;
        list.AddRectFilled(P(0,0), P(18,18), blue, 4);
        list.AddTriangleFilled(P(2,9), P(5,3), P(8,7), white);
        list.AddTriangleFilled(P(10,7), P(13,3), P(16,9), white);
        list.AddRectFilled(P(3,6), P(15,14), white, 3 * scale);
        list.AddCircleFilled(P(7,10), scale, blue);
        list.AddCircleFilled(P(12,10), scale, blue);
        if (!iconOnly) list.AddText(origin + new Vector2(height + 3, (height - ImGui.GetTextLineHeight()) / 2), ImGui.GetColorU32(ImGuiCol.Text), "Discord");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Join our Discord");
        return clicked;
    }
}