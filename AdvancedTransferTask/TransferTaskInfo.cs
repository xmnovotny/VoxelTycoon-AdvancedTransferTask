using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Tasks;
using XMNUtils;

namespace AdvancedTransferTask
{
    public class TransferTaskInfo
    {
        public bool IsIncomplete { get; private set; }
        
        private readonly Dictionary<Item, int> _capacityPerItem = new();

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

        private void CalculateCapacityPerItem()
        {
            _capacityPerItem.Clear();
            ImmutableList<VehicleUnit> units = _task.GetAvailableUnits();
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
                    // ReSharper disable once PossibleLossOfFraction
                    _capacityPerItem.AddIntToDict(unit.Storage.Item,  Mathf.RoundToInt(Mathf.Ceil(unit.Storage.Capacity * _percent / 100)));
                }
            }
        }
    }
}