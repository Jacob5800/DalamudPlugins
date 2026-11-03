using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BiSplanner.Items;

public partial class ItemDataService
{
    private Dictionary<uint, HashSet<uint>>? instanceSources;
    private readonly Dictionary<uint, IReadOnlyList<string>> instanceSourceLabels = [];

    public IReadOnlyList<string> GetInstanceSources(uint itemId)
    {
        itemId %= 1_000_000;
        if (instanceSourceLabels.TryGetValue(itemId, out var cached)) return cached;
        if (instanceSources == null)
        {
            var index = new Dictionary<uint, HashSet<uint>>();
            void Add(uint item, uint duty)
            {
                if (item == 0 || duty == 0) return;
                if (!index.TryGetValue(item, out var duties)) index[item] = duties = [];
                duties.Add(duty);
            }
            try
            {
                foreach (var row in CsvLoader.LoadResource<DungeonBossChest>(CsvLoader.DungeonBossChestResourceName, true, out _, out _, dataManager.GameData, dataManager.GameData.Options.DefaultExcelLanguage))
                    Add(row.ItemId, row.ContentFinderConditionId);
                foreach (var row in CsvLoader.LoadResource<DungeonBossDrop>(CsvLoader.DungeonBossDropResourceName, true, out _, out _, dataManager.GameData, dataManager.GameData.Options.DefaultExcelLanguage))
                    Add(row.ItemId, row.ContentFinderConditionId);
                foreach (var row in CsvLoader.LoadResource<DungeonDrop>(CsvLoader.DungeonDropItemResourceName, true, out _, out _, dataManager.GameData, dataManager.GameData.Options.DefaultExcelLanguage))
                    Add(row.ItemId, row.ContentFinderConditionId);
                var chests = CsvLoader.LoadResource<DungeonChest>(CsvLoader.DungeonChestResourceName, true, out _, out _, dataManager.GameData, dataManager.GameData.Options.DefaultExcelLanguage).ToDictionary(c => c.RowId, c => c.ContentFinderConditionId);
                foreach (var row in CsvLoader.LoadResource<DungeonChestItem>(CsvLoader.DungeonChestItemResourceName, true, out _, out _, dataManager.GameData, dataManager.GameData.Options.DefaultExcelLanguage))
                    if (chests.TryGetValue(row.ChestId, out var duty)) Add(row.ItemId, duty);
            }
            catch (Exception ex) { logger.Error(ex, "Could not load complete instance loot sources"); }
            instanceSources = index;
        }
        var ids = new HashSet<uint>(instanceSources.GetValueOrDefault(itemId) ?? []);
        // Include the instance that drops an exchange token or a gear coffer.
        foreach (var coffer in ItemsCoffers[itemId])
            if (instanceSources.TryGetValue(coffer.ItemId, out var duties)) ids.UnionWith(duties);
        var sheet = dataManager.GetExcelSheet<ContentFinderCondition>();
        var labels = ids.Select(id => sheet.GetRowOrDefault(id))
            .Where(row => row.HasValue).Select(row => row!.Value.Name.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().Order().ToArray();
        instanceSourceLabels[itemId] = labels;
        return labels;
    }
}
