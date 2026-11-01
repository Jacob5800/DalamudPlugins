using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Workshoppa.Voyages;

internal sealed record VoyageLoot(uint ItemId,string Name,uint Quantity,bool Hq,uint VendorUnitValue);
internal sealed record VoyageRecord(string Key,ulong House,VesselKind Kind,string Vessel,long ReturnTime,DateTimeOffset CollectedAt,List<VoyageLoot> Loot)
{
    public ulong VendorValue => Loot.Aggregate(0UL,(total,item)=>total+(ulong)item.Quantity*item.VendorUnitValue);
}
internal sealed class TrackedVoyage
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public VesselKind Kind { get; set; }
    public long ReturnTime { get; set; }
    public bool Notified { get; set; }
}
internal sealed class VoyageHistoryData
{
    public List<VoyageRecord> Records { get; set; } = new();
    public Dictionary<string,TrackedVoyage> Fleet { get; set; } = new();
}
internal sealed class VoyageHistory
{
    private readonly string path;
    private readonly Action<string> error;
    private VoyageHistoryData data = new();
    private readonly Dictionary<string,VoyageRecord> pending = new();
    private bool writable = true;
    public string Status { get; private set; } = "History records observed collections only.";
    public IReadOnlyList<VoyageRecord> Records => data.Records;
    public IReadOnlyCollection<TrackedVoyage> Fleet => data.Fleet.Values;
    public VoyageHistory(string path,Action<string> error)
    {
        this.path=path;this.error=error;
        try
        {
            if(File.Exists(path))data=JsonSerializer.Deserialize<VoyageHistoryData>(File.ReadAllText(path))??throw new InvalidDataException("Empty history file");
            data.Records??=new();data.Fleet??=new();
        }
        catch(Exception ex){writable=false;Status="History could not load; original file preserved. "+ex.Message;error(Status);}
    }
    private void Save()
    {
        if(!writable)return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary=path+".tmp";
            File.WriteAllText(temporary,JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
            File.Move(temporary,path,true);
        }
        catch(Exception ex){Status="History save failed: "+ex.Message;error(Status);}
    }
    public void Observe(ulong house,IReadOnlyList<Vessel> roster,long now,Func<Vessel,List<VoyageLoot>?> readRewards)
    {
        bool changed=false;
        foreach(var vessel in roster)
        {
            var key=vessel.Key(house);
            if(pending.TryGetValue(key,out var collection) && vessel.ReturnTime!=collection.ReturnTime)
            {
                if(!data.Records.Any(r=>r.Key==collection.Key&&r.ReturnTime==collection.ReturnTime))
                {data.Records.Add(collection with{CollectedAt=DateTimeOffset.UtcNow});changed=true;}
                pending.Remove(key);
            }
            if(vessel.ReturnTime>0 && vessel.ReturnTime<=now && readRewards(vessel) is {} loot)
                pending[key]=new(key,house,vessel.Kind,vessel.Name,vessel.ReturnTime,default,loot);
            if(vessel.ReturnTime>0)
            {
                if(!data.Fleet.TryGetValue(key,out var tracked)||tracked.ReturnTime!=vessel.ReturnTime)
                {data.Fleet[key]=new(){Key=key,Name=vessel.Name,Kind=vessel.Kind,ReturnTime=vessel.ReturnTime};changed=true;}
            }
            else if(data.Fleet.Remove(key))changed=true;
        }
        if(changed)Save();
    }
    public void NotifyDue(long now,bool enabled,Action<string> notify)
    {
        if(!enabled)return;
        bool changed=false;
        foreach(var v in data.Fleet.Values.Where(v=>v.ReturnTime>0&&v.ReturnTime<=now&&!v.Notified))
        {notify($"{v.Kind} {v.Name} has returned. Open its voyage log to collect rewards.");v.Notified=true;changed=true;}
        if(changed)Save();
    }
    public void Export(string destination)
    {
        static string Csv(string text)=>"\""+text.Replace("\"","\"\"")+"\"";
        var rows=new List<string>{"CollectedAt,Workshop,Type,Vessel,ReturnTime,ItemId,Item,Quality,Quantity,NpcUnitValue,NpcTotalValue"};
        foreach(var record in data.Records)
            foreach(var item in record.Loot)
                rows.Add($"{record.CollectedAt:O},{record.House:X16},{record.Kind},{Csv(record.Vessel)},{record.ReturnTime},{item.ItemId},{Csv(item.Name)},{(item.Hq?"HQ":"NQ")},{item.Quantity},{item.VendorUnitValue},{(ulong)item.Quantity*item.VendorUnitValue}");
        File.WriteAllLines(destination,rows);
        Status="Loot history exported to "+destination;
    }
}
