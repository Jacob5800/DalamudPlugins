using System;
using System.IO;
using System.Text.Json;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TataruBrawler;

internal sealed class RewardTracker
{
    private readonly string path;
    private readonly IGameGui gui;
    private readonly RewardSession model;
    private long nextSample, nextSave;
    private bool dirty, savingDisabled;
    public RewardTotals Session => model.Session;
    public RewardTotals Total => model.Total;
    public string? Error { get; private set; }

    public RewardTracker(string path, IGameGui gui)
    {
        this.path = path;
        this.gui = gui;
        var totals = new RewardTotals();
        try
        {
            if (File.Exists(path))
            {
                totals = JsonSerializer.Deserialize<RewardTotals>(File.ReadAllText(path))
                    ?? throw new InvalidDataException("Empty reward totals");
                if (totals.Games < 0 || totals.Mgp < 0 || totals.Mgc < 0)
                    throw new InvalidDataException("Negative reward totals");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            Error = "Saved reward totals could not be loaded. The existing file will be preserved; session tracking is still available.";
            savingDisabled = true;
            totals = new RewardTotals();
        }
        model = new RewardSession(totals);
    }

    public unsafe void Tick(long now, bool loggedIn, uint territory)
    {
        if (now < nextSample) return;
        nextSample = now + 100;
        if (!loggedIn || territory is not (1359 or 1360))
        {
            model.EndVisit();
            if (dirty && now >= nextSave) Flush();
            return;
        }
        bool inGame = territory == 1359;
        bool resultVisible = false;
        if (inGame)
        {
            var agents = AgentModule.Instance();
            var resultAgent = agents == null ? null : agents->GetAgentByInternalId(AgentId.KTGResult);
            var result = gui.GetAddonByName<AtkUnitBase>("KTGResult");
            resultVisible = resultAgent != null && result != null && result->IsVisible && resultAgent->GetAddonId() == result->Id;
        }
        var inventory = InventoryManager.Instance();
        var currency = inventory == null ? null : inventory->GetInventoryContainer(InventoryType.Currency);
        bool currencyReady = currency != null && currency->IsLoaded;
        long? mgp = !currencyReady ? null : inventory->GetInventoryItemCount(29, false, false, false);
        long? mgc = !currencyReady ? null : inventory->GetInventoryItemCount(52322, false, false, false);
        if (model.Observe(now, inGame, resultVisible, mgp, mgc)) dirty = true;
        if (dirty && now >= nextSave) Flush();
    }

    public void EndVisit() => model.EndVisit();

    public void Flush()
    {
        if (!dirty || savingDisabled) return;
        nextSave = Environment.TickCount64 + 30000;
        var temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, JsonSerializer.Serialize(Total));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
            dirty = false;
            nextSave = Environment.TickCount64 + 2000;
            Error = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = "Reward totals could not be saved; tracking continues and saving will retry.";
        }
    }
}
