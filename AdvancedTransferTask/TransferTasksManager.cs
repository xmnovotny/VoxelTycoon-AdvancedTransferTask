using System;
using System.Collections.Generic;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.Serialization;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Tasks;
using XMNUtils;

namespace AdvancedTransferTask
{
    [HarmonyPatch]
    [SchemaVersion(1)]
    public class TransferTasksManager : LazyManager<TransferTasksManager>
    {
        private readonly Dictionary<TransferTask, int> _tasksPercents = new();
        private readonly Dictionary<TransferTask, TransferTaskInfo> _cachedTaskInfo = new();

        public int? GetTaskPercent(TransferTask task)
        {
            if (_tasksPercents.TryGetValue(task, out int percent))
            {
                return percent;
            }

            return null;
        }

        public void SetTaskPercent(TransferTask task, int? percent)
        {
            FileLog.Log($"SetPercent: {percent}");
            if (percent.HasValue)
            {
                if (percent < 1)
                {
                    percent = 1;
                }
                else if (percent > 99)
                {
                    percent = 99;
                }

                _tasksPercents[task] = percent.Value;
            }
            else
            {
                _tasksPercents.Remove(task);
            }
            RemoveCachedValues(task);
        }

        [CanBeNull]
        private TransferTaskInfo GetTransferTaskInfo(TransferTask task)
        {
            if (!_cachedTaskInfo.TryGetValue(task, out TransferTaskInfo taskInfo))
            {
                if (_tasksPercents.TryGetValue(task, out int percent))
                {
                    taskInfo = new TransferTaskInfo(task, percent);
                }

                _cachedTaskInfo[task] = taskInfo;
            }

            return taskInfo;
        }

        private static int GetSumOfLoadedItems([NotNullAttribute] TransferTask task,[NotNullAttribute] Item item)
        {
            int result = 0;
            ImmutableUniqueList<VehicleUnit> units = task.GetTargetUnits();
            for (int i = 0; i < units.Count; i++)
            {
                VehicleUnit unit = units[i];
                if (unit.Storage?.Item == item)
                {
                    result += unit.Storage.Count;
                }
            }

            return result;
        } 

        private static Dictionary<Item, int> GetSumOfLoadedItems([NotNullAttribute] TransferTask task)
        {
            Dictionary<Item, int> result = new();
            ImmutableUniqueList<VehicleUnit> units = task.GetTargetUnits();
            for (int i = 0; i < units.Count; i++)
            {
                VehicleUnit unit = units[i];
                if (unit.Storage != null)
                {
                    result.AddIntToDict(unit.Storage.Item, unit.Storage.Count);
                }
            }

            return result;
        } 

        /**
         * Called when vehicle is loading, result is whether it can be loaded
         */
        private bool TryAcceptTransaction(TransferTask task, VehicleUnit unit, NodeStoragePair source, NodeStoragePair target,
            IStorageTransaction transaction, bool preview)
        {
            TransferTaskInfo taskInfo = GetTransferTaskInfo(task);
            if (taskInfo != null)
            {
                Item item = transaction.Item;
                int? capacity = taskInfo.GetCapacity(item, true);
                if (!taskInfo.IsIncomplete && capacity != null)
                {
                    int loadedItems = GetSumOfLoadedItems(task, item);
                    if (loadedItems >= capacity)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /**
         * Called when vehicle is unloading, if result is null, no unload is made
         */
        private IStorageTransaction CreateTransaction(TransferTask task, VehicleUnit unit, NodeStoragePair source, NodeStoragePair target, IStorageTransaction transaction)
        {
            TransferTaskInfo taskInfo = GetTransferTaskInfo(task);
            if (taskInfo != null)
            {
                Item item = transaction.Item;
                int? capacity = taskInfo.GetCapacity(item);
                if (capacity != null)
                {
                    int loadedItems = GetSumOfLoadedItems(task, item);
                    if (loadedItems <= capacity)
                    {
                        return null;
                    }
                }
            }

            return transaction;
        }

        private bool IsUnloadingComplete(TransferTask task)
        {
            TransferTaskInfo taskInfo = GetTransferTaskInfo(task);
            if (taskInfo != null)
            {
                Dictionary<Item, int> loadedItems = GetSumOfLoadedItems(task);
                foreach (KeyValuePair<Item, int> capacity in taskInfo.GetCapacityPerItem())
                {
                    if (loadedItems.TryGetValue(capacity.Key, out int loaded) && loaded > capacity.Value)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        
        private bool IsLoadingComplete(TransferTask task)
        {
            TransferTaskInfo taskInfo = GetTransferTaskInfo(task);
            if (taskInfo != null)
            {
                Dictionary<Item, int> loadedItems = GetSumOfLoadedItems(task);
                foreach (KeyValuePair<Item, int> capacity in taskInfo.GetCapacityPerItem())
                {
                    if (!loadedItems.TryGetValue(capacity.Key, out int loaded) || loaded < capacity.Value)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal void Write(StateBinaryWriter writer)
        {
            writer.WriteInt(_tasksPercents.Count);
            foreach (KeyValuePair<TransferTask, int> taskPercent in _tasksPercents)
            {
                Vehicle vehicle = taskPercent.Key.Vehicle;
                int rootTaskIndex = -1;
                int taskIndex = -1;
                if (vehicle != null)
                {
                    rootTaskIndex = taskPercent.Key.ParentTask.GetIndex();
                    taskIndex = taskPercent.Key.GetIndex();
                }

                if (vehicle != null && rootTaskIndex != -1 && taskIndex != -1)
                {
                    writer.WriteInt(vehicle.Id);
                    writer.WriteInt(rootTaskIndex);
                    writer.WriteInt(taskIndex);
                    writer.WriteInt(taskPercent.Value);
                }
                else
                {
                    writer.WriteInt(-1);
                }
            }
        }

        internal void Read(StateBinaryReader reader)
        {
            _tasksPercents.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int vehicleIndex = reader.ReadInt();
                if (vehicleIndex <= -1) continue;
                
                Vehicle vehicle = LazyManager<VehicleManager>.Current.FindById(vehicleIndex);
                if (vehicle == null) continue;

                int rootTaskIndex = reader.ReadInt();
                int taskIndex = reader.ReadInt();
                int value = reader.ReadInt();

                try
                {
                    SubTask subTask = vehicle.Schedule?.GetSubTask(rootTaskIndex, taskIndex);
                    if (subTask is TransferTask transferTask)
                    {
                        _tasksPercents[transferTask] = value;
                    }
                }
                catch
                {
                    Logger.Log(LogType.Error, "Error getting subtask while loading.");
                }
            }
        }

        private void RemoveCachedValues(TransferTask task)
        {
            _cachedTaskInfo.Remove(task);
        }
        
        #region HARMONY

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VehicleTask), "OnRemove")]
        private static void TransferTask_OnRemove_pof(VehicleTask __instance)
        {
            if (__instance is TransferTask transferTask)
            {
                Current.RemoveCachedValues(transferTask);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnitsTask), "OnStart")]
        private static void TransferTask_OnStart_pof(UnitsTask __instance)
        {
            if (__instance is TransferTask transferTask)
            {
                Current.RemoveCachedValues(transferTask);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TransferTask), "OnStop")]
        private static void TransferTask_OnStop_pof(TransferTask __instance)
        {
            Current._cachedTaskInfo.Remove(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TransferTask), "GetVersion")]
        private static void TransferTask_GetVersion_pof(TransferTask __instance, ref int __result)
        {
            if (Current._tasksPercents.TryGetValue(__instance, out int percent))
            {
                __result = __result * -0x5AAAAAD7 + percent;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TransferTask), "IsUnloading")]
        private static void TransferTask_IsUnloading_prf(TransferTask __instance, out bool __state)
        {
            __state = false;
            if (__instance.UnloadMode == TransferMode.Full && Current._tasksPercents.ContainsKey(__instance))
            {
                __state = true;
                __instance.UnloadMode = TransferMode.Partial;
            }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(TransferTask), "IsUnloading")]
        private static void TransferTask_IsUnloading_fin(TransferTask __instance, bool __state, ref bool __result)
        {
            if (__state)
            {
                __instance.UnloadMode = TransferMode.Full;
                if (__result == false)
                {
                    __result = !Current.IsUnloadingComplete(__instance);
                }
            }
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TransferTask), "IsLoading")]
        private static void TransferTask_IsLoading_prf(TransferTask __instance, out bool __state)
        {
            __state = false;
            if (__instance.LoadMode == TransferMode.Full && Current._tasksPercents.ContainsKey(__instance))
            {
                __state = true;
                __instance.LoadMode = TransferMode.Partial;
            }
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(TransferTask), "IsLoading")]
        private static void TransferTask_IsLoading_fin(TransferTask __instance, bool __state, ref bool __result)
        {
            if (__state)
            {
                __instance.LoadMode = TransferMode.Full;
                if (__result == false)
                {
                    __result = !Current.IsLoadingComplete(__instance);
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TransferTask), "TryAcceptTransaction")]
        private static bool TransferTask_TryAcceptTransaction_prf(TransferTask __instance, out bool __result, VehicleUnit unit, NodeStoragePair source, NodeStoragePair target, IStorageTransaction transaction, bool preview)
        {
            __result = Current.TryAcceptTransaction(__instance, unit, source, target, transaction, preview);
            return __result;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TransferTask), "CreateTransaction")]
        private static void TransferTask_CreateTransaction_pof(TransferTask __instance, ref IStorageTransaction __result, VehicleUnit unit, NodeStoragePair source, NodeStoragePair target)
        {
            if (__result != null)
            {
                __result = Current.CreateTransaction(__instance, unit, source, target, __result);
            }
        }
        #endregion
    }
}