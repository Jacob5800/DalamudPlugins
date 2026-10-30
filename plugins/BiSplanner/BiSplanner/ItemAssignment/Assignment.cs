using BiSplanner.Gear;
using System.Collections.Generic;

namespace BiSplanner.ItemAssignment
{
    public class Assignment
    {
        // null if gearpieces unassigned
        public uint? ItemId { get; set; }
        public List<uint> ItemMateria { get; set; } = [];
        public List<Gearpiece> Gearpieces { get; set; } = [];
    }
}
