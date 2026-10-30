using BiSplanner.Gear;
using System.Collections.Generic;

namespace BiSplanner.Import
{
    public struct ImportGearsetsResult
    {
        public GearsetImportStatusType StatusType;
        public List<Gearset>? Gearsets;
        public int SkippedDuplicates;
    }
}
