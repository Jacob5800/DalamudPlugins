using BiSplanner.Gear;
using BiSplanner.ItemAssignment;
using BiSplanner.Items;
using BiSplanner.Services;
using BiSplanner.Services.Configuration;
using System.Collections.Generic;

namespace BiSplanner.Factories
{
    public class ItemAssignmentSolverFactory(
        ITypedLogger<ItemAssigmentSolver> logger,
        IItemDataService itemDataService,
        IMateriaFactory materiaFactory,
        IMateriaGroupFactory materiaGroupFactory,
        IConfigurationService configurationService
        ) : IItemAssignmentSolverFactory
    {
        private readonly ITypedLogger<ItemAssigmentSolver> logger = logger;
        private readonly IItemDataService itemDataService = itemDataService;
        private readonly IMateriaFactory materiaFactory = materiaFactory;
        private readonly IMateriaGroupFactory materiaGroupFactory = materiaGroupFactory;
        private readonly IConfigurationService configurationService = configurationService;

        public IItemAssignmentSolver? LastCreatedSolver { get; private set; } = null;

        public IItemAssignmentSolver Create(
            IEnumerable<Gearset> allGearsets,
            IEnumerable<Gearset> assignableGearsets,
            List<InventoryItem> inventoryItems
            )
        {
            var solver = new ItemAssigmentSolver(
                logger,
                allGearsets,
                assignableGearsets,
                inventoryItems,
                itemDataService,
                materiaFactory,
                materiaGroupFactory,
                configurationService
                );
            LastCreatedSolver = solver;
            return solver;
        }
    }

    public interface IItemAssignmentSolverFactory
    {
        public IItemAssignmentSolver Create(
            IEnumerable<Gearset> allGearsets,
            IEnumerable<Gearset> assignableGearsets,
            List<InventoryItem> inventoryItems
            );

        public IItemAssignmentSolver? LastCreatedSolver { get; }
    }
}
