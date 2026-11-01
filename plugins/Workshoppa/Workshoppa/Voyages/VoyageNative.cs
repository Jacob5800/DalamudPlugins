// Native voyage event shapes and component inventory locations adapted from AutoRetainer.
// Copyright (c) 2023 Puni.sh. BSD-3-Clause; see AutoRetainer-LICENSE.md and VOYAGE-REFERENCES.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Utility;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib;
using Lumina.Excel.Sheets;
using Lumina.Excel;
using Lumina.Text.ReadOnly;

namespace Workshoppa.Voyages;

internal sealed unsafe class VoyageNative : IVoyageGame
{
    private readonly IGameGui gui;
    private readonly IClientState client;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly IDataManager data;
    private readonly Dictionary<string,string> labels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Regex? repairPrompt;
    private readonly uint repairMaterial;
    public IReadOnlyList<Destination> Destinations { get; }
    internal IReadOnlyDictionary<uint,string> MapNames { get; }
    public VoyageNative(IGameGui gui, IClientState client, ICondition condition, IObjectTable objects, IDataManager data, IPluginLog log)
    {
        (this.gui,this.client,this.condition,this.objects,this.data)=(gui,client,condition,objects,data);
        string[] wanted = { "Airship Management", "Submersible Management", "View previous voyage log", "Repair airship components", "Repair submersible components", "Deploy airship on exploratory voyage", "Deploy submersible on subaquatic voyage", "Quit", "Nothing." };
        var english = data.GetExcelSheet<Addon>(ClientLanguage.English);
        var local = data.GetExcelSheet<Addon>();
        foreach(var name in wanted)
        {
            var row = english.FirstOrDefault(r=> string.Equals(r.Text.ExtractText().Trim(),name,StringComparison.OrdinalIgnoreCase));
            if (row.RowId != 0) labels[name] = local.GetRow(row.RowId).Text.ExtractText().Trim();
            else if (client.ClientLanguage == ClientLanguage.English) labels[name] = name;
        }
        foreach(var sheet in new[]{"custom/002/CmnDefCompanyCommanderBoard_00258","custom/004/HouFixCompanySubmarine_00447"})
        {
            var en=data.GetExcelSheet<Dialogue>(ClientLanguage.English,sheet);
            var localized=data.GetExcelSheet<Dialogue>(null,sheet);
            foreach(var row in en)
            {
                var text=row.Value.ExtractText().Trim();
                if(wanted.Contains(text,StringComparer.OrdinalIgnoreCase)) labels[text]=localized.GetRow(row.RowId).Value.ExtractText().Trim();
            }
        }
        // Root options are script-generated; translations established by AutoRetainer's Lang catalog.
        if(client.ClientLanguage==ClientLanguage.Japanese)
        { labels["Airship Management"]="飛空艇の管理";labels["Submersible Management"]="潜水艦の管理";labels["Quit"]="やめる"; }
        else if(client.ClientLanguage==ClientLanguage.German)
        { labels["Airship Management"]="Luftschiff verwalten";labels["Submersible Management"]="Tauchboot verwalten";labels["Quit"]="Beenden"; }
        else if(client.ClientLanguage==ClientLanguage.French)
        { labels["Airship Management"]="Contrôle aérien";labels["Submersible Management"]="Contrôle sous-marin";labels["Quit"]="Annuler"; }
        repairPrompt = data.GetRegex<Addon>(6587,r=>r.Text,log);
        repairMaterial = data.GetExcelSheet<Item>(ClientLanguage.English).FirstOrDefault(r=>r.Name.ExtractText().Equals("Magitek repair materials",StringComparison.OrdinalIgnoreCase)).RowId;
        MapNames = data.GetExcelSheet<SubmarineMap>().Where(r=>r.Name.ExtractText().Length>0).ToDictionary(r=>r.RowId,r=>r.Name.ExtractText());
        Destinations = data.GetExcelSheet<SubmarineExploration>().Where(r=>r.Destination.ExtractText().Length>0 && r.RankReq>0)
            .Select(r=>new Destination(r.RowId,VesselKind.Submarine,r.Map.RowId,r.Destination.ExtractText().Trim(),r.RankReq))
            .Concat(data.GetExcelSheet<AirshipExplorationPoint>().Where(r=>r.Name.ExtractText().Length>0 && r.RankReq>0)
                .Select(r=>new Destination(r.RowId,VesselKind.Airship,0,r.Name.ExtractText().Trim(),r.RankReq))).ToArray();
    }
    [Sheet("custom/002/CmnDefCompanyCommanderBoard_00258")]
    private readonly struct Dialogue(ExcelPage page,uint offset,uint row):IExcelRow<Dialogue>
    {
        public uint RowId=>row;
        public ExcelPage ExcelPage=>page;
        public uint RowOffset=>offset;
        public ReadOnlySeString Value=>page.ReadString(offset+4,offset);
        static Dialogue IExcelRow<Dialogue>.Create(ExcelPage page,uint offset,uint row)=>new(page,offset,row);
    }
    private AtkUnitBase* Addon(string name)
    { var a = gui.GetAddonByName<AtkUnitBase>(name); return a != null && a->IsVisible && a->IsReady ? a : null; }
    public bool Available => client.IsLoggedIn && new uint[]{423,424,425,653,984}.Contains(client.TerritoryType)
        && !condition[ConditionFlag.BetweenAreas] && !condition[ConditionFlag.BetweenAreas51] && !condition[ConditionFlag.InCombat]
        && HousingManager.Instance()!=null && HousingManager.Instance()->WorkshopTerritory!=null;
    public ulong Character => objects.LocalPlayer?.GameObjectId ?? 0;
    public ulong House => Available ? HousingManager.Instance()->WorkshopTerritory->HouseId.Id : 0;
    public bool Cutscene => condition[ConditionFlag.WatchingCutscene] || condition[ConditionFlag.WatchingCutscene78] || condition[ConditionFlag.OccupiedInCutSceneEvent];
    private string[] Entries()
    {
        var addon = (AddonSelectString*)Addon("SelectString");
        if (addon == null) return Array.Empty<string>();
        var menu = &addon->PopupMenu.PopupMenu;
        if (menu->EntryCount is <1 or >32) return Array.Empty<string>();
        var result = new List<string>();
        for(int i=0;i<menu->EntryCount;i++)
            result.Add(menu->EntryNames[i].HasValue ? SeString.Parse(menu->EntryNames[i].Value).TextValue.Trim() : "");
        return result.ToArray();
    }
    private bool Has(string english) => labels.TryGetValue(english,out var label) && Entries().Contains(label,StringComparer.Ordinal);
    private bool Select(string english)
    {
        if (!labels.TryGetValue(english,out var label)) return false;
        var entries = Entries(); int index = Array.FindIndex(entries,e=>e==label);
        if(index<0) return false;
        Addon("SelectString")->FireCallbackInt(index); return true;
    }
    public bool Root => Has("Airship Management") && Has("Submersible Management");
    public bool AnyWindow => Addon("SelectString")!=null || Result || Detail || Editor || MapSelect || RepairWindow || Addon("SelectYesno")!=null || Cutscene;
    public bool Result => Addon("AirShipExplorationResult")!=null;
    public bool Detail => Addon("AirShipExplorationDetail")!=null;
    public bool Editor => Addon("AirShipExploration")!=null;
    public bool MapSelect => Addon("SubmarineExplorationMapSelect")!=null;
    public bool RepairWindow => Addon("CompanyCraftSupply")!=null || Addon("AirShipPartsMenu")!=null;
    public bool VesselMenu => Has("View previous voyage log") && Has("Quit");
    public bool Picker(VesselKind kind) => !Root && !VesselMenu && Has("Nothing.") && !Result && !Detail && !RepairWindow;
    public IReadOnlyList<Vessel> Roster(VesselKind kind)
    {
        var result = new List<Vessel>();
        if(!Available) return result;
        var workshop = HousingManager.Instance()->WorkshopTerritory;
        for(int i=0;i<4;i++)
        {
            string name; long time; uint rank;
            if(kind==VesselKind.Airship)
            { var v=workshop->Airship.Data[i]; name=v.NameString.ToString(); time=v.ReturnTime; rank=v.RankId; }
            else
            { var v=workshop->Submersible.DataPointers[i].Value; if(v==null)continue; name=v->NameString.ToString();time=v->ReturnTime;rank=v->RankId; }
            if(string.IsNullOrEmpty(name)) continue;
            var parts = new ushort[4]; bool ready=true;
            for(int j=0;j<4;j++)
            { var slot=Component(kind,i,j); if(slot==null || slot->ItemId==0)ready=false; else parts[j]=slot->Condition; }
            result.Add(new Vessel(kind,i,name,time,rank,parts,ready));
        }
        return result;
    }
    private static InventoryItem* Component(VesselKind kind,int vessel,int part)
    {
        if(vessel is <0 or >3 || part is <0 or >3) return null;
        var inventory=InventoryManager.Instance(); if(inventory==null)return null;
        var bag=inventory->GetInventoryContainer(kind==VesselKind.Airship?InventoryType.HousingInteriorPlacedItems1:InventoryType.HousingInteriorPlacedItems2);
        int index=(kind==VesselKind.Airship?30:0)+vessel*5+part;
        return bag!=null && bag->IsLoaded && index<bag->Size ? bag->GetInventorySlot(index):null;
    }
    public bool SelectedVesselMatches(Vessel vessel)
    {
        if(!Available)return false;
        var w=HousingManager.Instance()->WorkshopTerritory;
        return vessel.Kind==VesselKind.Airship ? w->Airship.ActiveAirshipId==vessel.Slot
            : w->Submersible.DataPointers[4].Value!=null && w->Submersible.DataPointers[4].Value==w->Submersible.DataPointers[vessel.Slot].Value;
    }
    public uint SelectedMap => AgentSubmersibleExploration.Instance()==null ? uint.MaxValue : AgentSubmersibleExploration.Instance()->MapId;
    public bool SelectKind(VesselKind kind) => Root && Select(kind==VesselKind.Airship?"Airship Management":"Submersible Management");
    public bool SelectVessel(Vessel v)
    {
        if(!Picker(v.Kind))return false;
        var entries=Entries();
        var matches=entries.Select((text,index)=>(text,index)).Where(e=>e.text.StartsWith(v.Name+" ",StringComparison.Ordinal)||e.text.StartsWith(v.Name+" (",StringComparison.Ordinal)||e.text.StartsWith(v.Name+".",StringComparison.Ordinal)).ToArray();
        if(matches.Length!=1)return false;
        Addon("SelectString")->FireCallbackInt(matches[0].index); return true;
    }
    public bool Collect() { var a=Addon("AirShipExplorationResult");if(a==null)return false;a->FireCallbackInt(0);return true; }
    public bool PreviousLog()=>VesselMenu && Select("View previous voyage log");
    private static bool ButtonReady(AtkUnitBase* a,int nodeIndex)
    {
        if(a==null || a->UldManager.NodeList==null || a->UldManager.NodeListCount<=nodeIndex)return false;
        var node=a->UldManager.NodeList[nodeIndex]; if(node==null)return false;
        var button=node->GetAsAtkComponentButton();return button!=null && button->IsEnabled;
    }
    public bool Repeat(out string? error)
    {
        error=null;var a=Addon("AirShipExplorationResult");if(a==null)return false;
        if(!ButtonReady(a,3))return false;
        a->FireCallbackInt(1);return true;
    }
    private static string ValueString(AtkUnitBase* a,int i)
        =>a!=null && a->AtkValues!=null && i<a->AtkValuesCount && a->AtkValues[i].Type is AtkValueType.String or AtkValueType.ManagedString
            && a->AtkValues[i].String.Value!=null ? SeString.Parse(a->AtkValues[i].String.Value).TextValue : "";
    private static bool Ratios(AtkUnitBase* a,int fuel,int distance,out string? error)
    {
        error=null;
        if(!VoyagePolicy.TryRatio(ValueString(a,fuel),out var need,out var have) || !VoyagePolicy.TryRatio(ValueString(a,distance),out var range,out var max))
        {error="Voyage fuel/range data could not be verified. No deployment submitted.";return false;}
        if(need==0 || range==0 || need>have || range>max)
        {error=$"Voyage cannot deploy: fuel {need}/{have}, distance {range}/{max}. Check tanks, range and route.";return false;}
        return true;
    }
    public bool Deploy(out string? error)
    {error=null;var a=Addon("AirShipExplorationDetail");if(a==null)return false;
        if(ValueString(a,1).Length==0 || ValueString(a,2).Length==0)return false;
        if(!Ratios(a,1,2,out error))return false;a->FireCallbackInt(0);return true;}
    public bool QuitVessel()=>VesselMenu && Select("Quit");
    public bool QuitPicker()=>Select("Nothing.");
    public bool OpenRepair(VesselKind kind)=>VesselMenu && Select(kind==VesselKind.Airship?"Repair airship components":"Repair submersible components");
    public bool RequestRepair(Vessel vessel,int part,out string? error)
    {
        error=null;var a=Addon("CompanyCraftSupply");if(a==null)a=Addon("AirShipPartsMenu");if(a==null)return false;
        if(Addon("SelectYesno")!=null){error="An unrelated confirmation is already open. Close it before repairing vessels.";return false;}
        if(repairMaterial==0 || InventoryManager.Instance()==null || InventoryManager.Instance()->GetInventoryItemCount(repairMaterial)==0)
        {error="Magitek repair materials are unavailable. No repair or deployment submitted.";return false;}
        var item=Component(vessel.Kind,vessel.Slot,part);if(item==null||item->ItemId==0||item->Condition!=0){error="Repair component changed.";return false;}
        var args=stackalloc AtkValue[6];args[0]=new(){Type=AtkValueType.Int,Int=3};args[1]=default;args[2]=new(){Type=AtkValueType.Int,Int=part};args[3]=default;args[4]=default;args[5]=default;
        a->FireCallback(6,args,true);return true;
    }
    public bool ConfirmRepair(Vessel vessel,int part,out string? error)
    {
        error=null;var a=(AddonSelectYesno*)Addon("SelectYesno");if(a==null)return false;
        var slot=Component(vessel.Kind,vessel.Slot,part);if(slot==null||slot->ItemId==0||slot->Condition!=0){error="Repair component changed before confirmation.";return false;}
        var name=data.GetExcelSheet<Item>().GetRow(slot->GetBaseItemId()).Name.ExtractText();
        var text=a->PromptText==null?"":a->PromptText->NodeText.ExtractText();
        if(repairPrompt==null || !repairPrompt.IsMatch(text) || name.Length==0 || !text.Contains(name,StringComparison.OrdinalIgnoreCase))
        {error="The open confirmation does not match the requested vessel repair.";return false;}
        if(a->YesButton==null || !a->YesButton->IsEnabled){error="Repair confirmation is disabled; check materials and permissions.";return false;}
        a->AtkUnitBase.FireCallbackInt(0);return true;
    }
    public bool CloseRepair(){var a=Addon("CompanyCraftSupply");if(a==null)a=Addon("AirShipPartsMenu");if(a==null)return false;a->FireCallbackInt(5);return true;}
    public bool OpenRoute()=>VesselMenu && (Select("Deploy airship on exploratory voyage")||Select("Deploy submersible on subaquatic voyage"));
    public bool PickMap(uint map,out string? error)
    {
        error=null;var a=Addon("SubmarineExplorationMapSelect");if(a==null)return false;
        if(a->AtkValues==null||a->AtkValuesCount<6){error="Voyage map data unavailable.";return false;}
        bool found=false;
        for(int i=0;i<7;i++)
        {
            int at=3+i*3;if(at+2>=a->AtkValuesCount || a->AtkValues[at].Type==0)break;
            if(a->AtkValues[at].Type!=AtkValueType.UInt || a->AtkValues[at+2].Type!=AtkValueType.UInt){error="Unexpected voyage map row.";return false;}
            if(a->AtkValues[at].UInt==map && a->AtkValues[at+2].UInt<=a->AtkValues[1].UInt)found=true;
        }
        if(!found){error="Configured submarine map is not available.";return false;}
        var args=stackalloc AtkValue[3];args[0]=new(){Type=AtkValueType.Int,Int=2};args[1]=default;args[2]=new(){Type=AtkValueType.UInt,UInt=map};a->FireCallback(3,args,true);return true;
    }
    public int SelectedCount(VesselKind kind)
    {
        if(kind==VesselKind.Submarine){var a=AgentSubmersibleExploration.Instance();return a==null?-1:a->SelectedPointsCount;}
        return VoyagePolicy.TryRatio(RouteDistance,out var n,out _) ? (n==0?0:-1) : -1;
    }
    public string RouteDistance=>ValueString(Addon("AirShipExploration"),7);
    [StructLayout(LayoutKind.Explicit,Size=32)] private struct PointInput { [FieldOffset(0)]public PointContext* Context;[FieldOffset(16)]public int Index;[FieldOffset(24)]public byte Flag; }
    [StructLayout(LayoutKind.Explicit,Size=176)] private struct PointContext { [FieldOffset(168)]public PointFlags* Flags; }
    [StructLayout(LayoutKind.Explicit,Size=176)] private struct PointFlags { [FieldOffset(172)]public int Value; }
    public bool PickPoint(Destination point,out string? error)
    {
        error=null;var a=Addon("AirShipExploration");if(a==null)return false;
        // Native displayed rows, not sheet IDs, determine this event's zero-based index.
        if(a->AtkValues==null || a->AtkValuesCount<20){error="Voyage destination layout is unavailable.";return false;}
        int selected=-1;
        for(int i=0;i<74;i++)
        {
            int at=13+i*7;if(at+6>=a->AtkValuesCount || a->AtkValues[at].Type==0)break;
            if(ValueString(a,at+1).Trim()!=point.Name && ValueString(a,at+2).Trim()!=point.Name)continue;
            if(selected>=0){error="Ambiguous voyage destination name.";return false;}
            if(a->AtkValues[at+6].Type!=AtkValueType.UInt || a->AtkValues[at+6].UInt is not (0 or 1)){error=$"Destination {point.Name} is locked or unavailable.";return false;}
            selected=i;
        }
        if(selected<0){error=$"Configured destination {point.Name} is absent from the open map.";return false;}
        var flags=new PointFlags{Value=0x0FFFFFFF};var context=new PointContext{Flags=&flags};var input=new PointInput{Context=&context,Index=selected,Flag=0};AtkEvent ev=default;
        a->ReceiveEvent((AtkEventType)35,0,&ev,(AtkEventData*)&input);return true;
    }
    public bool ReviewRoute(out string? error)
    {
        error=null;var a=Addon("AirShipExploration");if(a==null)return false;
        if(!Ratios(a,6,7,out error))return false;
        if(!ButtonReady(a,2)){error="The configured voyage route cannot be confirmed.";return false;}
        a->FireCallbackInt(0);return true;
    }
}
