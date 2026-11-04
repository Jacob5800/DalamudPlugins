using Dalamud.Bindings.ImGui;
using Dalamud.Configuration;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using System.Text.RegularExpressions;

namespace RollKnockout;

public sealed class Config : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public int Players { get; set; } = 8;
    public int Maximum { get; set; } = 999;
    public bool HighestOut { get; set; }
    public bool AutoAnnounce { get; set; }
    public int Channel { get; set; }
}

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IChatGui chat;
    private readonly IPlayerState player;
    private readonly IPluginLog log;
    private readonly Config config;
    private readonly Match match = new();
    private bool open;
    private string manualName = "";
    private int manualRoll = 1;
    private string announcement = "";
    private string excludeName = "";
    private DateTime nextPublish;
    private static readonly Regex RollPattern = new(@"\broll(?:s|ed)?\b[^\d]{0,24}(?<roll>\d{1,6})\s*\(out of (?<max>\d{1,6})\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IChatGui chat, IPlayerState player, IPluginLog log)
    {
        this.pi = pi; this.commands = commands; this.chat = chat; this.player = player; this.log = log;
        config = pi.GetPluginConfig() as Config ?? new();
        commands.AddHandler("/rollknockout", new CommandInfo((_, _) => Toggle()) { HelpMessage = "Open Roll Knockout." });
        pi.UiBuilder.Draw += Draw; pi.UiBuilder.OpenMainUi += Toggle; pi.UiBuilder.OpenConfigUi += Toggle;
        chat.ChatMessage += OnChat;
    }
    private void Toggle() => open = !open;
    private void OnChat(IHandleableChatMessage message)
    {
        // Only native random-result messages count; ordinary player text cannot spoof a roll.
        if (!match.Active || message.LogKind != XivChatType.RandomNumber) return;
        var text = message.Message.TextValue;
        var m = RollPattern.Match(text);
        if (!m.Success || !int.TryParse(m.Groups["roll"].Value, out var value) ||
            !int.TryParse(m.Groups["max"].Value, out var maximum)) return;
        var payload = message.Message.Payloads.OfType<PlayerPayload>().FirstOrDefault()
            ?? message.Sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        var name = payload?.PlayerName ?? message.Sender.TextValue;
        if (Regex.IsMatch(text, @"^\s*(?:Random!\s*)?You\s+roll\b", RegexOptions.IgnoreCase))
            name = player.IsLoaded ? player.CharacterName : "";
        else if (string.IsNullOrWhiteSpace(name))
        {
            var n = Regex.Match(text, @"^\s*(?:Random!\s*)?(?<name>.+?)\s+roll(?:s|ed)?\b", RegexOptions.IgnoreCase);
            if (n.Success) name = n.Groups["name"].Value;
        }
        if (match.Accept(name, value, maximum) && match.Ready && config.AutoAnnounce) Resolve();
    }
    private void Resolve()
    {
        announcement = match.Resolve();
        chat.Print(announcement, "Roll Knockout");
        if (config.AutoAnnounce) Publish();
    }
    private void Publish()
    {
        if (announcement.Length == 0 || DateTime.UtcNow < nextPublish) return;
        try
        {
            var prefix = config.Channel switch { 1 => "/say", 2 => "/shout", _ => "/p" };
            var text = Chat.SanitiseText("[Roll Knockout] " + announcement);
            // Keep within the game's UTF-8 chat limit, even with large tied groups.
            if (System.Text.Encoding.UTF8.GetByteCount(text) > 450)
            { chat.Print("Announcement too long. Copy it or announce a shorter summary manually.", "Roll Knockout"); return; }
            Chat.SendMessage(prefix + " " + text); nextPublish = DateTime.UtcNow.AddSeconds(2);
        }
        catch (Exception ex) { log.Error(ex, "Roll Knockout announcement failed."); }
    }
    private void Draw()
    {
        if (!open) return;
        if (ImGui.Begin("Roll Knockout", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped("First unique players to roll join the match. Only their first roll counts. English random-result text is supported; manual entry is available below.");
            ImGui.BeginDisabled(match.Active);
            var count = config.Players; var max = config.Maximum; var high = config.HighestOut;
            if (ImGui.InputInt("Players", ref count)) { config.Players = Math.Clamp(count, 2, 100); Save(); }
            if (ImGui.InputInt("Roll maximum", ref max)) { config.Maximum = Math.Clamp(max, 2, 999999); Save(); }
            if (ImGui.Checkbox("Highest roll is out (otherwise lowest)", ref high)) { config.HighestOut = high; Save(); }
            if (ImGui.Button("Start match")) { match.Start(config.Players, config.Maximum, config.HighestOut); announcement = ""; }
            ImGui.EndDisabled();
            if (match.Active) { ImGui.SameLine(); if (ImGui.Button("Stop")) match.Stop(); }
            var auto = config.AutoAnnounce; var channel = config.Channel;
            if (ImGui.Checkbox("Automatically resolve and announce completed rounds", ref auto)) { config.AutoAnnounce = auto; Save(); }
            if (ImGui.Combo("Announcement channel", ref channel, "Party\0Say\0Shout\0")) { config.Channel = channel; Save(); }
            ImGui.Separator();
            ImGui.TextWrapped($"Round {match.Round}: {match.Rolls.Count}/{match.Expected} rolls. {match.Status}");
            if (match.Rolls.Count > 0)
            {
                ImGui.Text($"Lowest: {match.Rolls.Values.Min()} | Highest: {match.Rolls.Values.Max()}");
                foreach (var r in match.Rolls.OrderBy(x => x.Value)) ImGui.TextUnformatted($"{r.Key}: {r.Value}");
            }
            ImGui.BeginDisabled(!match.Ready);
            if (ImGui.Button("Resolve round")) Resolve();
            ImGui.EndDisabled();
            ImGui.TextWrapped(announcement);
            if (ImGui.CollapsingHeader("Exclude / mark player out"))
            {
                ImGui.InputText("Excluded player name", ref excludeName, 100);
                if (ImGui.Button("Mark out / ignore rolls")) match.MarkOut(excludeName);
                ImGui.TextWrapped("Use their full character name. Players eliminated by a resolved round are excluded automatically. Exclusions reset with a new match.");
                foreach (var name in match.Out) ImGui.TextUnformatted($"Out: {name}");
            }
            ImGui.BeginDisabled(announcement.Length == 0);
            if (ImGui.Button("Publish result")) Publish();
            ImGui.SameLine(); if (ImGui.Button("Copy result")) ImGui.SetClipboardText(announcement);
            ImGui.EndDisabled();
            if (ImGui.CollapsingHeader("Manual roll entry"))
            {
                ImGui.InputText("Player name", ref manualName, 100);
                ImGui.InputInt("Roll", ref manualRoll);
                if (ImGui.Button("Record roll") && match.Accept(manualName, manualRoll, match.Maximum) && match.Ready && config.AutoAnnounce) Resolve();
            }
        }
        ImGui.End();
    }
    private void Save() => pi.SavePluginConfig(config);
    public void Dispose()
    {
        chat.ChatMessage -= OnChat; pi.UiBuilder.Draw -= Draw;
        pi.UiBuilder.OpenMainUi -= Toggle; pi.UiBuilder.OpenConfigUi -= Toggle;
        commands.RemoveHandler("/rollknockout");
    }
}
