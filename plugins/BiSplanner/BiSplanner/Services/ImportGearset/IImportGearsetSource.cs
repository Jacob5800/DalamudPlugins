using BiSplanner.Gear;
using BiSplanner.Import;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BiSplanner.Services.ImportGearset
{
    public interface IImportGearsetSource
    {
        public ImportGearsetSourceType SourceType { get; }
        public Task<List<Gearset>> ImportGearsets(string importString);
    }
}
