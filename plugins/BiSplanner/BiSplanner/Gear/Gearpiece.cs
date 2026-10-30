using BiSplanner.Gear.Melds;
using BiSplanner.Gear.Prerequisites;
using BiSplanner.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BiSplanner.Gear
{
    public delegate void GearpieceChangeHandler();

    public class Gearpiece : ICollectableItem
    {
        private bool isCollected;
        private bool collectLock;
        private readonly ITypedLogger<Gearpiece> logger;
        public uint ItemId { get; set; }
        public string ItemName { get; set; }
        public GearpieceType GearpieceType { get; set; }
        public PrerequisiteNode? PrerequisiteTree { get; set; }
        public bool IsCollected
        {
            get => isCollected;
            set
            {
                if (CollectLock)
                {
                    logger.Warning($"Attempted to {(value ? "collect" : "uncollect")} locked gearpiece \"{ItemName}\"");
                    return;
                }

                isCollected = value;
                PrerequisiteTree?.IsCollected = value;

                handleIsCollectedChange();
            }
        }
        public bool CollectLock
        {
            get => collectLock;
            set
            {
                foreach (var materia in ItemMateria)
                    materia.CollectLock = value;

                PrerequisiteTree?.CollectLock = value;

                if (value == collectLock)
                    return;

                logger.Info($"{(value ? "locking" : "unlocking")} gearpiece \"{ItemName}\"");
                collectLock = value;

                triggerGearpieceChange();
            }
        }
        public MateriaGroup ItemMateria { get; init; }

        // Runtime observation from the exact inventory copy chosen by the assignment solver.
        public IReadOnlyList<uint>? ObservedMateriaIds { get; set; }

        public Dictionary<int, uint> GetWrongMateria()
        {
            var wrong = new Dictionary<int, uint>();
            if (ObservedMateriaIds is null)
                return wrong;

            var remaining = ObservedMateriaIds.Where(id => id != 0).ToList();
            var missing = new List<int>();
            foreach (var (index, required) in ItemMateria.Index())
            {
                // Consume matching types first, independent of physical meld order.
                if (!remaining.Remove(required.ItemId))
                    missing.Add(index);
            }
            for (var i = 0; i < Math.Min(missing.Count, remaining.Count); i++)
                wrong[missing[i]] = remaining[i];
            return wrong;
        }

        public Gearpiece(
            ITypedLogger<Gearpiece> logger,
            uint itemId,
            string itemName,
            GearpieceType gearpieceType,
            PrerequisiteNode? prerequisiteTree,
            MateriaGroup itemMateria,
            bool isCollected = false,
            bool collectLock = false
        )
        {
            this.logger = logger;
            ItemId = itemId;
            ItemName = itemName;
            GearpieceType = gearpieceType;
            PrerequisiteTree = prerequisiteTree;
            this.isCollected = isCollected;
            this.collectLock = collectLock;
            ItemMateria = itemMateria;

            if (PrerequisiteTree is PrerequisiteNode node)
                node.OnPrerequisiteChange += triggerGearpieceChange;

            ItemMateria.OnMateriaGroupChange += triggerGearpieceChange;

            if (CollectLock)
            {
                if (!isCollected)
                    ItemMateria.UnmeldAllMateria(respectCollectLock: false);
                else
                    PrerequisiteTree?.SetIsCollectedLocked(true);
            }
            else
            {
                if (isCollected && PrerequisiteTree is not null)
                    PrerequisiteTree.IsCollected = true;
            }
        }

        public CollectionStatusType CollectionStatus
        {
            get
            {
                if (IsCollected)
                    return ItemMateria.All(m => m.CollectionStatus == CollectionStatusType.ObtainedComplete)
                        ? CollectionStatusType.ObtainedComplete
                        : CollectionStatusType.ObtainedPartial;
                if (PrerequisiteTree is not PrerequisiteNode tree)
                    return CollectionStatusType.NotObtainable;
                if (tree.CollectionStatus >= CollectionStatusType.Obtainable)
                    return CollectionStatusType.Obtainable;
                return tree.CollectionStatus;
            }
        }

        private void handleIsCollectedChange()
        {
            if (!IsCollected)
                ItemMateria.UnmeldAllMateria();

            if (PrerequisiteTree != null)
                PrerequisiteTree.IsCollected = IsCollected;

            triggerGearpieceChange();
        }

        public event GearpieceChangeHandler? OnGearpieceChange;

        private void triggerGearpieceChange() =>
            OnGearpieceChange?.Invoke();

        public IEnumerable<ItemRequirementOwned> ItemRequirements(Gearset parentGearset, bool includeUncollectedItemMateria)
        {
            yield return new ItemRequirementOwned(
                new(
                    ItemId,
                    CollectionStatus,
                    RequirementType.Gearpiece
                    ),
                parentGearset,
                this
                );

            // ignore materia if uncollected item materia is not enabled
            if (IsCollected || includeUncollectedItemMateria)
            {
                foreach (var materia in ItemMateria)
                {
                    yield return new ItemRequirementOwned(
                        new(
                            materia.ItemId,
                            materia.CollectionStatus,
                            RequirementType.Materia
                            ),
                        parentGearset,
                        this
                        );
                }
            }

            if (!IsCollected && PrerequisiteTree is not null)
            {
                foreach (var prereq in PrerequisiteTree.ActivePrerequisiteNodes)
                    foreach (var req in prereq.GetItemRequirements())
                        yield return new(
                            req,
                            parentGearset,
                            this
                        );
            }
        }

        public void SetIsCollectedLocked(bool toCollect)
        {
            PrerequisiteTree?.SetIsCollectedLocked(toCollect);

            if (!toCollect)
                ItemMateria.UnmeldAllMateria(respectCollectLock: false);

            if (toCollect == IsCollected && CollectLock)
                return;

            if (!CollectLock)
                CollectLock = true;

            logger.Info($"Gearpiece \"{ItemName}\" locked as {(toCollect ? "collected" : "uncollected")}");

            isCollected = toCollect;

            triggerGearpieceChange();
        }

        public List<uint> CollectLockItemIds()
        {
            if (CollectLock)
                return [ItemId];

            return PrerequisiteTree?.CollectLockItemIds() ?? [];
        }
    }
}
