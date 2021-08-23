
namespace AdvancedTransferTask
{
    using System.Collections.Generic;
    using JetBrains.Annotations;
    using UnityEngine;
    using VoxelTycoon;
    using VoxelTycoon.Tracks;
    using VoxelTycoon.Tracks.Tasks;
    using XMNUtils;

    public class TransferTaskInfo
    {
        public bool IsIncomplete { get; private set; }
        
        private readonly Dictionary<Item, int> _capacityPerItem = new();
        private readonly Dictionary<Item, int> _tmpCapacityPerItem = new();

        private readonly TransferTask _task;
        private readonly int _percent;

        public TransferTaskInfo([NotNullAttribute] TransferTask task, int percent)
        {
            _task = task;
            _percent = percent;
            CalculateCapacityPerItem();
        }

        public int? GetCapacity(Item item, bool refreshWhenIncomplete = false)
        {
            if (refreshWhenIncomplete && IsIncomplete)
            {
                CalculateCapacityPerItem();
            }

            return _capacityPerItem.TryGetValue(item, out int capacity) ? capacity : null;
        }

        public IReadOnlyDictionary<Item, int> GetCapacityPerItem()
        {
            return _capacityPerItem;
        }

        public static int CalculateFinalCapacity(int percent, int capacity)
        {
            // ReSharper disable once PossibleLossOfFraction
            return Mathf.RoundToInt(Mathf.Ceil(capacity * percent / 100));
        }

        private void CalculateCapacityPerItem()
        {
            _capacityPerItem.Clear();
            _tmpCapacityPerItem.Clear();
            ImmutableUniqueList<VehicleUnit> units = _task.GetTargetUnits();
            IsIncomplete = false;
            for (int i = 0; i < units.Count; i++)
            {
                VehicleUnit unit = units[i];
                if (unit.Storage == null)
                {
                    IsIncomplete = true;
                }
                else
                {
                    _tmpCapacityPerItem.AddIntToDict(unit.Storage.Item,  unit.Storage.Capacity);
                }
            }

            foreach (var capacity in _tmpCapacityPerItem)
            {
                _capacityPerItem[capacity.Key] = CalculateFinalCapacity(_percent, capacity.Value);
            }
            _tmpCapacityPerItem.Clear();
        }
    }
}