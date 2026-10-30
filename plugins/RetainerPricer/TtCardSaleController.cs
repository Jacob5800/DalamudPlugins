using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using NativeObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace RetainerPricer;

internal sealed unsafe class TtCardSaleController(IDalamudPluginInterface pi, IGameGui gui, IDataManager data,
    IObjectTable objects, ITargetManager targets, IClientState client, IPlayerState player, ICondition condition,
    PluginConfig config, NativeMarketBridge bridge, Func<bool> otherBusy)
{
    private static readonly Vector3 VendorPosition = new(-56.1f,1.6f,16.6f);
    private static readonly InventoryType[] Bags = [InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4];
    public bool IsRunning { get; private set; }
    public string Status { get; private set; } = "TT card selling is idle.";
    private DateTimeOffset next, deadline, operationDeadline;
    private ulong owner;
    private bool teleported, navigationOwned, interacted, waitingDialog, awaitingSale;
    private uint selectedItem, beforeCount, selectedQuantity;
    private int selectedRow, sold;
    public void Start()
    {
        if (IsRunning || otherBusy()) { Status = "Finish other Retainer Pricer operations first."; return; }
        if (!client.IsLoggedIn || player.ContentId==0 || condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty])
        { Status = "Log in and leave combat/duties before selling TT cards."; return; }
        if (!QuestManager.IsQuestComplete(65970)) { Status = "Unlock the Gold Saucer first."; return; }
        if (EligibleCards().Count==0) { Status = "No registered duplicate TT cards in carried inventory. Unregistered cards and Exceptions are kept."; return; }
        try
        {
            // Verify IPC availability before teleporting; mesh readiness is checked after arrival.
            if (pi.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning").InvokeFunc())
            { Status = "Another vnavmesh path is running; stop it before selling cards."; return; }
        }
        catch { Status = "Install and enable vnavmesh before selling TT cards."; return; }
        owner=player.ContentId; sold=0; teleported=navigationOwned=interacted=waitingDialog=awaitingSale=false;
        IsRunning=true; next=DateTimeOffset.MinValue; deadline=DateTimeOffset.UtcNow.AddMinutes(4);
        Status="Starting TT card sale trip to the Gold Saucer...";
    }
    public void Stop(string reason="TT card selling stopped.")
    {
        if(navigationOwned) try { pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction(); } catch { }
        navigationOwned=false; IsRunning=false; Status=reason;
    }
    private Dictionary<uint,(ushort Card,uint Count)> EligibleCards()
    {
        Dictionary<uint,(ushort,uint)> result=[];
        var inv=InventoryManager.Instance(); var ui=UIState.Instance();
        if(inv==null || ui==null) return result;
        foreach(var type in Bags)
        {
            var bag=inv->GetInventoryContainer(type); if(bag==null || !bag->IsLoaded) continue;
            for(int i=0;i<bag->Size;i++)
            {
                var slot=bag->GetInventorySlot(i); if(slot==null || slot->IsEmpty()) continue;
                uint id=slot->GetBaseItemId(); if(config.ExcludedItemIds.Contains(id)) continue;
                var row=data.GetExcelSheet<Item>().GetRowOrDefault(id);
                if(row is not {} item || item.ItemUICategory.RowId!=86 || item.AdditionalData.RowId is 0 or > ushort.MaxValue) continue;
                ushort card=(ushort)item.AdditionalData.RowId;
                if(!ui->IsTripleTriadCardUnlocked(card)) continue;
                var old=result.GetValueOrDefault(id); result[id]=(card,old.Item2+slot->GetQuantity());
            }
        }
        return result;
    }
    public void Update()
    {
        if(!IsRunning) return;
        var now=DateTimeOffset.UtcNow; if(now<next) return; next=now.AddMilliseconds(500);
        if(!client.IsLoggedIn || player.ContentId!=owner || otherBusy() || condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty])
        { Stop("TT card sale stopped: character, combat, duty or controller changed."); return; }
        if(now>deadline) { Stop("TT card sale timed out. Check travel and NPC windows."); return; }
        if(condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51] || condition[ConditionFlag.Casting]) return;
        try { Tick(now); } catch(Exception ex) { Stop("TT card sale stopped: " + ex.Message); }
    }
    private void Tick(DateTimeOffset now)
    {
        var local=objects.LocalPlayer; if(local==null) return;
        if(bridge.IsRetainerMenuVisible || bridge.IsRetainerSellListVisible || bridge.IsRetainerDialogueVisible || bridge.IsSellWindowVisible)
        { Stop("Close the active retainer interaction before the TT card trip."); return; }
        if(bridge.IsRetainerPickerVisible)
        { gui.GetAddonByName<AtkUnitBase>("RetainerList")->Close(true); return; }
        if(condition[ConditionFlag.OccupiedSummoningBell]) return;
        if(client.TerritoryType!=144)
        {
            if(teleported) return;
            var telepo=Telepo.Instance(); if(telepo==null) { Stop("Teleport is unavailable."); return; }
            telepo->UpdateAetheryteList();
            var destination=telepo->TeleportList.ToArray().FirstOrDefault(x=>x.TerritoryId==144);
            if(destination.AetheryteId==0 || !telepo->Teleport(destination.AetheryteId,destination.SubIndex))
            { Stop("Gold Saucer teleport unavailable or rejected. No cards were sold."); return; }
            teleported=true; Status="Teleporting to the Gold Saucer..."; return;
        }
        if(Vector3.Distance(local.Position,VendorPosition)>4)
        {
            if(!pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady").InvokeFunc()) { Status="Waiting for the Gold Saucer navigation mesh..."; return; }
            if(!navigationOwned)
            {
                if(pi.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning").InvokeFunc()) { Stop("Another navigation path started."); return; }
                if(!pi.GetIpcSubscriber<Vector3,bool,bool>("vnavmesh.SimpleMove.PathfindAndMoveTo").InvokeFunc(VendorPosition,false))
                { Stop("No path to the Triple Triad Trader was found."); return; }
                navigationOwned=true; Status="Walking to the Triple Triad Trader...";
            }
            return;
        }
        if(navigationOwned) { pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction(); navigationOwned=false; }
        var npc=objects.FirstOrDefault(x=>x.BaseId==1016294);
        if(npc==null) { Stop("Triple Triad Trader is not loaded."); return; }
        if(!interacted)
        {
            targets.Target=npc;
            TargetSystem.Instance()->InteractWithObject((NativeObject*)npc.Address);
            interacted=true; operationDeadline=now.AddSeconds(30); return;
        }
        if(targets.Target?.GameObjectId!=npc.GameObjectId) { Stop("The selected NPC changed; no further cards were sold."); return; }
        var exchange=gui.GetAddonByName<AtkUnitBase>("TripleTriadCoinExchange");
        if(exchange==null || !exchange->IsReady || !exchange->IsVisible)
        {
            var menu=gui.GetAddonByName<AtkUnitBase>("SelectIconString");
            if(menu!=null && menu->IsReady && menu->IsVisible) { Callback(menu,1); next=now.AddSeconds(1); }
            if(now>operationDeadline) Stop("The trader's card exchange did not open.");
            return;
        }
        var eligible=EligibleCards();
        if(awaitingSale)
        {
            uint remaining=eligible.GetValueOrDefault(selectedItem).Count;
            if(remaining<beforeCount)
            { sold+=(int)(beforeCount-remaining); awaitingSale=waitingDialog=false; next=now.AddSeconds(1); Status=$"Sold {sold} duplicate TT card(s)."; }
            else if(now>operationDeadline) Stop("TT card sale was not confirmed in inventory. Check the game error; no retry was submitted.");
            return;
        }
        if(waitingDialog)
        {
            var dialog=gui.GetAddonByName<AddonShopCardDialog>("ShopCardDialog");
            if(dialog==null || !dialog->IsVisible || !dialog->IsReady) { if(now>operationDeadline) Stop("Card quantity dialog did not open."); return; }
            if(!eligible.TryGetValue(selectedItem,out var selected) || selected.Count!=beforeCount)
            { Stop("Selected card inventory changed before confirmation."); return; }
            if(dialog->CardQuantityInput==null || dialog->CardQuantityInput->Data.Max<(int)selectedQuantity)
            { Stop("Card sale quantity changed; no sale confirmed."); return; }
            dialog->CardQuantityInput->SetValue((int)selectedQuantity);
            if(dialog->CardQuantityInput->Data.Value!=(int)selectedQuantity) { Stop("Card quantity was not accepted."); return; }
            if (!NativeUiButton.Click((AtkUnitBase*)dialog, dialog->GetComponentButtonById(16)))
            { if (now > operationDeadline) Stop("The card Sell button was not ready; no retry was submitted."); return; }
            awaitingSale=true; operationDeadline=now.AddSeconds(15); return;
        }
        // Exchange row offsets corroborated by ECommons ReaderTripleTriadCoinExchange.
        if(exchange->AtkValues==null || exchange->AtkValuesCount<2 || exchange->AtkValues[1].UInt>40) { Stop("Unsupported TT card exchange layout."); return; }
        int count=(int)exchange->AtkValues[1].UInt;
        if(count>0 && exchange->AtkValuesCount<164+count) { Stop("Incomplete TT card exchange rows."); return; }
        for(int i=0;i<count;i++)
        {
            uint id=exchange->AtkValues[164+i].UInt;
            var match=eligible.FirstOrDefault(x=>x.Key==id || x.Value.Card==id);
            if(match.Key==0) continue;
            uint quantity=Math.Min(match.Value.Count,exchange->AtkValues[124+i].UInt);
            if(quantity==0) continue;
            var currency=CurrencyManager.Instance();
            if(currency!=null && currency->HasItem(29))
            {
                uint cap=currency->GetItemMaxCount(29), balance=currency->GetItemCount(29);
                if(cap>0 && (ulong)balance+(ulong)quantity*exchange->AtkValues[84+i].UInt>cap)
                { Stop("Selling these cards would exceed the MGP cap; spend MGP first."); return; }
            }
            selectedItem=match.Key; beforeCount=match.Value.Count; selectedQuantity=quantity; selectedRow=i;
            Callback(exchange,0,selectedRow); waitingDialog=true; operationDeadline=now.AddSeconds(10); return;
        }
        exchange->Close(true);
        Stop($"TT card sale complete: {sold} duplicate card(s) sold. Unregistered cards and Exceptions kept.");
    }
    private static void Callback(AtkUnitBase* addon, params int[] values)
    {
        AtkValue* args=stackalloc AtkValue[values.Length];
        for(int i=0;i<values.Length;i++) args[i]=new AtkValue { Type=AtkValueType.Int, Int=values[i] };
        addon->FireCallback((uint)values.Length,args,true);
    }
}
