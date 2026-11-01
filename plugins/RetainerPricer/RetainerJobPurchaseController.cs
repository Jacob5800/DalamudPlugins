using Dalamud.Utility;
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

internal sealed unsafe class RetainerJobPurchaseController(IDalamudPluginInterface pi, IGameGui gui, IDataManager data,
    IObjectTable objects, ITargetManager targets, IClientState client, IPlayerState player, ICondition condition,
    NativeMarketBridge bridge, Func<bool> otherBusy)
{
    private readonly uint itemId = data.GetExcelSheet<Item>(Dalamud.Game.ClientLanguage.English).First(x=>x.Name.ToString()=="Modern Vocation").RowId;
    private readonly uint npcId = data.GetExcelSheet<ENpcResident>(Dalamud.Game.ClientLanguage.English).First(x=>x.Singular.ToString()=="Frydwyb").RowId;
    private readonly uint ventureId = data.GetExcelSheet<Item>(Dalamud.Game.ClientLanguage.English).First(x=>x.Name.ToString()=="Venture").RowId;
    public bool IsRunning { get; private set; }
    public bool MissingNavigationPopup { get; set; }
    public string Status { get; private set; } = "Buy one Modern Vocation for 40 Ventures.";
    private DateTimeOffset next, deadline;
    private bool navigationOwned, teleported, interacted, submitted;
    private ulong owner;
    private string lastChoice = "";
    public void Start()
    {
        MissingNavigationPopup=false;
        if(!pi.InstalledPlugins.Any(p=>p.IsLoaded && string.Equals(p.InternalName,"vnavmesh",StringComparison.OrdinalIgnoreCase)))
        { MissingNavigationPopup=true;Status="Install and enable vnavmesh first.";return; }
        if(IsRunning || otherBusy()) { Status="Finish other Retainer Pricer operations first."; return; }
        if(!client.IsLoggedIn || condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty]) { Status="Leave combat/duties first."; return; }
        if(client.ClientLanguage!=Dalamud.Game.ClientLanguage.English) { Status="This purchase currently requires English game menus."; return; }
        var inv=InventoryManager.Instance();
        if(inv==null || inv->GetInventoryItemCount(itemId)>0) { Status="Modern Vocation is already carried, or inventory is unavailable."; return; }
        if(inv->GetEmptySlotsInBag()==0) { Status="Make space in carried inventory first."; return; }
        if(inv->GetInventoryItemCount(ventureId)<40) { Status="You need 40 Ventures."; return; }
        try { if(pi.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning").InvokeFunc()) { Status="Stop the current navigation path first.";return; } }
        catch { MissingNavigationPopup=true;Status="Install and enable vnavmesh first.";return; }
        lastChoice="";owner=player.ContentId;navigationOwned=teleported=interacted=submitted=false;IsRunning=true;
        next=DateTimeOffset.MinValue;deadline=DateTimeOffset.UtcNow.AddMinutes(4);Status="Travelling to Frydwyb in Limsa Lominsa...";
    }
    public void Stop(string reason="Modern Vocation purchase stopped.")
    {
        if(navigationOwned)try{pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction();}catch{}
        navigationOwned=false;IsRunning=false;Status=reason;
    }
    public void Update()
    {
        if(!IsRunning)return;var now=DateTimeOffset.UtcNow;if(now<next)return;next=now.AddMilliseconds(600);
        if(!client.IsLoggedIn || player.ContentId!=owner || otherBusy() || condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty]) { Stop("Purchase stopped: character or controller changed, combat or duty started.");return; }
        if(now>deadline){Stop("Purchase timed out. Check NPC dialogue and inventory.");return;}
        if(condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51] || condition[ConditionFlag.Casting])return;
        try{Tick();}catch(Exception ex){Stop("Purchase stopped: "+ex.Message);}
    }
    private void Tick()
    {
        var inv=InventoryManager.Instance();if(inv==null)return;
        if(inv->GetInventoryItemCount(itemId)>0){Stop("Purchased Modern Vocation. Assign the retainer job manually at a summoning bell.");return;}
        if(submitted){Status="Waiting for purchase confirmation in inventory...";return;}
        if(bridge.IsRetainerPickerVisible || bridge.IsRetainerSellListVisible || bridge.IsSellWindowVisible){Stop("Close retainer windows before starting this trip.");return;}
        if(client.TerritoryType!=129)
        {
            if(teleported)return;var t=Telepo.Instance();if(t==null)return;t->UpdateAetheryteList();
            var d=t->TeleportList.ToArray().FirstOrDefault(x=>x.TerritoryId==129);
            if(d.AetheryteId==0 || !t->Teleport(d.AetheryteId,d.SubIndex)){Stop("Limsa Lominsa teleport unavailable.");return;}
            teleported=true;return;
        }
        var npc=objects.FirstOrDefault(x=>x.BaseId==npcId);var local=objects.LocalPlayer;
        if(npc==null || local==null){Status="Waiting for Frydwyb to load...";return;}
        if(Vector3.Distance(local.Position,npc.Position)>3)
        {
            if(!pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady").InvokeFunc())return;
            if(!navigationOwned){if(pi.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning").InvokeFunc()){Stop("Another navigation path started.");return;}
                if(!pi.GetIpcSubscriber<Vector3,bool,bool>("vnavmesh.SimpleMove.PathfindAndMoveTo").InvokeFunc(npc.Position,false)){Stop("No path to Frydwyb.");return;}navigationOwned=true;}
            return;
        }
        if(navigationOwned){pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction();navigationOwned=false;}
        if(!interacted){targets.Target=npc;TargetSystem.Instance()->InteractWithObject((NativeObject*)npc.Address);interacted=true;return;}
        if(targets.Target?.GameObjectId!=npc.GameObjectId){Stop("NPC target changed.");return;}
        var yes=gui.GetAddonByName<AddonSelectYesno>("SelectYesno");
        if(yes!=null && yes->IsReady && yes->IsVisible)
        {
            string text=yes->PromptText==null?"":yes->PromptText->NodeText.ExtractText();
            if(!text.Contains("Modern Vocation",StringComparison.OrdinalIgnoreCase) || !text.Contains("40")){Stop("Unexpected confirmation; no purchase confirmed.");return;}
            if(inv->GetInventoryItemCount(ventureId)<40){Stop("Insufficient Ventures.");return;}
            yes->AtkUnitBase.FireCallbackInt(0);submitted=true;deadline=DateTimeOffset.UtcNow.AddSeconds(15);return;
        }
        if(bridge.TrySelectRetainerMenuEntry(t=>t!=lastChoice && (t.Contains("Inquire about retainer jobs",StringComparison.OrdinalIgnoreCase) || t.Contains("Purchase a copy of Modern Vocation",StringComparison.OrdinalIgnoreCase)),out var choice,out _))
        {lastChoice=choice;Status=choice;return;}
        var talk=gui.GetAddonByName<AtkUnitBase>("Talk");
        var stage=AtkStage.Instance();
        if(talk!=null && talk->IsReady && talk->IsVisible && stage!=null)
        {
            var click=stackalloc AtkEvent[1];
            click[0]=new AtkEvent { Listener=(AtkEventListener*)talk,Target=&stage->AtkEventTarget,State=new(){StateFlags=(AtkEventStateFlags)132} };
            var eventData=stackalloc AtkEventData[1];eventData[0]=default;
            talk->ReceiveEvent(AtkEventType.MouseDown,0,click,eventData);
            talk->ReceiveEvent(AtkEventType.MouseClick,0,click,eventData);
            talk->ReceiveEvent(AtkEventType.MouseUp,0,click,eventData);
        }
        Status="Waiting for the Retainer Vocate menu...";
    }
}
