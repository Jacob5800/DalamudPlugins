// Component UI flow adapted from Argus PartsInterop (MIT), ofnature; see packaged notices.
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using Workshoppa.Voyages.Optimization.Model;
using Workshoppa.Voyages.Optimization.Data;
namespace Workshoppa.Voyages;
internal sealed unsafe partial class VoyageNative
{
    internal static VesselType ModelKind(VesselKind kind)=>kind==VesselKind.Submarine?VesselType.Submarine:VesselType.Airship;
    internal uint[] ReadBuild(Vessel vessel)
    {
        if(!Available)return Array.Empty<uint>();
        var w=HousingManager.Instance()->WorkshopTerritory;
        if(vessel.Kind==VesselKind.Airship){var v=w->Airship.Data[vessel.Slot];return new uint[]{v.HullId,v.SternId,v.BowId,v.BridgeId};}
        var sub=w->Submersible.DataPointers[vessel.Slot].Value;
        return sub==null?Array.Empty<uint>():new uint[]{sub->HullId,sub->SternId,sub->BowId,sub->BridgeId};
    }
    internal HashSet<uint> OwnedParts(Vessel vessel,PlannerData tables)
    {
        var inventory=InventoryManager.Instance();var current=ReadBuild(vessel).ToHashSet();
        foreach(var part in tables.Parts(ModelKind(vessel.Kind)).Values)
        {uint item=PartItems.ItemFor(ModelKind(vessel.Kind),part.Id);if(item!=0&&inventory!=null&&inventory->GetInventoryItemCount(item,false,false)>0)current.Add(part.Id);}
        return current;
    }
    internal int FuelCount
    {get{var i=InventoryManager.Instance();return i==null?0:i->GetInventoryItemCount(10155,false,false);}}
    internal HashSet<uint> Unlocked(Vessel vessel,PlannerData tables,uint map)
    {
        var type=ModelKind(vessel.Kind);var result=new HashSet<uint>();
        if(type==VesselType.Submarine)
        {
            foreach(var sector in tables.DestinationsOf(type,map))if(sector.Id<=255&&HousingManager.IsSubmarineExplorationUnlocked((byte)sector.Id))result.Add(sector.Id);
        }
        else if(Editor && SelectedVesselMatches(vessel))
            for(int i=0;i<74;i++)
            {
                var a=Addon("AirShipExploration");int at=13+i*7;if(at+6>=a->AtkValuesCount||a->AtkValues[at].Type==0)break;
                if(a->AtkValues[at+6].Type!=AtkValueType.UInt||a->AtkValues[at+6].UInt is not(0 or 1))continue;
                string name=ValueString(a,at+1).Trim();var sector=tables.DestinationsOf(type,map).FirstOrDefault(s=>s.Name==name);
                if(sector!=null)result.Add(sector.Id);
            }
        return result;
    }
    internal PlannerData? PlanningTables { get; set; }
    private int swapStage,swapPart;
    private long swapDeadline;
    public void CancelBuild(){swapStage=0;}
    private AtkUnitBase* PartsAddon(){var a=Addon("CompanyCraftSupply");return a!=null?a:Addon("AirShipPartsMenu");}
    public bool EnsureBuild(Vessel vessel,IReadOnlyList<uint> parts,out string? error)
    {
        error=null;
        if(!SelectedVesselMatches(vessel)){error="Selected vessel changed before component swap.";return false;}
        if(swapStage!=0&&Environment.TickCount64>swapDeadline){error="Component swap timed out; inspect the vessel before retrying.";CancelBuild();return false;}
        var current=ReadBuild(vessel);
        if(current.Length!=4||parts.Count!=4){error="Vessel build data is unavailable.";return false;}
        if(swapStage==0)
        {
            if(PlanningTables==null){error="Build validation tables are unavailable.";return false;}
            Build planned;
            try{planned=Build.From(PlanningTables,ModelKind(vessel.Kind),(int)vessel.Rank,parts[0],parts[1],parts[2],parts[3]);}
            catch(Exception){error="Saved build contains unknown parts.";return false;}
            if(!planned.FitsCapacity||!planned.PartsAllowed||planned.Parts.Select((p,i)=>p.Slot==i).Any(ok=>!ok))
            {error="Saved build exceeds rank/capacity or has a part in the wrong slot.";return false;}
            if(current.SequenceEqual(parts))return true;
            if(!VesselMenu)return false;
            var inventory=InventoryManager.Instance();
            if(inventory==null||inventory->GetEmptySlotsInBag()<1){error="Free an inventory slot before changing vessel components.";return false;}
            for(int i=0;i<4;i++)if(current[i]!=parts[i])
            {
                uint item=PartItems.ItemFor(ModelKind(vessel.Kind),parts[i]);
                if(item==0||inventory->GetInventoryItemCount(item,false,false)<1){error="A planned replacement component is not in carried inventory.";return false;}
            }
            if(Select(vessel.Kind==VesselKind.Submarine?"Change submersible components":"Change airship components"))
            {swapStage=1;swapDeadline=Environment.TickCount64+30000;}
            return false;
        }
        if(swapStage==4){if(VesselMenu){swapStage=0;return current.SequenceEqual(parts);}return false;}
        var addon=PartsAddon();if(addon==null)return false;
        if(swapStage==1)
        {
            swapPart=Enumerable.Range(0,4).FirstOrDefault(i=>current[i]!=parts[i],-1);
            if(swapPart<0){addon->FireCallbackInt(5);swapStage=4;return false;}
            var args=stackalloc AtkValue[6];args[0]=new(){Type=AtkValueType.Int,Int=2};args[1]=new(){Type=AtkValueType.Int,Int=1};args[2]=new(){Type=AtkValueType.Int,Int=swapPart};args[3]=default;args[4]=default;args[5]=default;
            addon->FireCallback(6,args,true);swapStage=2;return false;
        }
        if(swapStage==2)
        {
            var picker=Addon("ContextIconMenu");if(picker==null)return false;
            if(picker->AtkValues==null||picker->AtkValuesCount<14||picker->AtkValues[4].Type!=AtkValueType.UInt){error="Unexpected component picker layout.";return false;}
            uint item=PartItems.ItemFor(ModelKind(vessel.Kind),parts[swapPart]);
            string wanted=data.GetExcelSheet<Item>().GetRow(item).Name.ExtractText();
            int count=(int)picker->AtkValues[4].UInt,index=-1;if(count is<1 or>100){error="Unexpected component list size.";return false;}
            static string Normalize(string text)=>new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            for(int i=0;i<count;i++)if(Normalize(ValueString(picker,13+i*8))==Normalize(wanted)){if(index>=0){error="Ambiguous replacement component entries.";return false;}index=i;}
            if(index<0){error="The component picker does not offer the planned part.";return false;}
            var args=stackalloc AtkValue[5];args[0]=default;args[1]=new(){Type=AtkValueType.Int,Int=index};args[2]=new(){Type=AtkValueType.UInt,UInt=item};args[3]=default;args[4]=default;
            picker->FireCallback(5,args,true);swapStage=3;return false;
        }
        if(swapStage==3)
        {if(Addon("ContextIconMenu")==null&&current[swapPart]==parts[swapPart]){swapStage=1;swapDeadline=Environment.TickCount64+30000;}return false;}
        return false;
    }
}
