
namespace AdvancedTransferTask
{
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
    
    [HarmonyPatch]
    [SchemaVersion(2)]
    public class TransferTasksManager : LazyManager<TransferTasksManager>
    {
        private readonly Dictionary<TransferTask, TransferTaskData> _tasksData = new();
        private readonly Dictionary<TransferTask, TransferTaskInfo> _cachedTaskInfo = new();
        private readonly Dictionary<TransferTask, Dictionary<Item, int>> _cachedLoadedItems = new();
        private readonly Dictionary<Storage, VehicleUnit> _storageToUnit = new();

        public Action<TransferTask> SettingsChanged;

        public int? GetTaskPercent(TransferTask task)
        {
            if (_tasksData.TryGetValue(task, out TransferTaskData data))
            {
                return data.Percent;
            }

            return null;
        }
        

        public void SetTaskPercent(TransferTask task, int? percent)
        {
            _tasksData.TryGetValue(task, out TransferTaskData oldData);
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

                TransferTaskData data;
                if (oldData == null)
                {
                    data = new TransferTaskData() {Percent = percent};
                }
                else
                {
                    data = oldData with {Percent = percent};
                }

                _tasksData[task] = data;
                OnTaskStart(task);
            }
            else
            {
                OnTaskStop(task);
                if (oldData != null)
                {
                    TransferTaskData data = oldData with {Percent = null};
                    if (data.IsDefault)
                    {
                        _tasksData.Remove(task);
                    }
                    else
                    {
                        _tasksData[task] = data;
                    }
                }
            }
            SettingsChanged?.Invoke(task);
        }

        public FullTransferOption GetTaskTransferOption(TransferTask task)
        {
            if (_tasksData.TryGetValue(task, out var taskData))
            {
                if (taskData.FullAny)
                {
                    return FullTransferOption.FullAny;
                }
            }

            return (task is LoadTask && task.LoadMode == TransferMode.Full || task is UnloadTask && task.UnloadMode == TransferMode.Full)
                ? FullTransferOption.FullAll
                : FullTransferOption.NoWait;
        }

        public void SetTaskTransferOption(TransferTask task, FullTransferOption transferOption)
        {
            if (transferOption == FullTransferOption.FullAny)
            {
                _tasksData[task] = _tasksData.GetValueOrDefault(task, new TransferTaskData()) with {FullAny = true};
            }
            else if (_tasksData.TryGetValue(task, out TransferTaskData data))
            {
                data = data with {FullAny = false};
                if (data.IsDefault)
                {
                    _tasksData.Remove(task);
                }
                else
                {
                    _tasksData[task] = data;
                }
            }

            if (task is LoadTask)
            {
                if (transferOption == FullTransferOption.NoWait)
                {
                    task.LoadMode = TransferMode.Partial;
                }
                else
                {
                    task.LoadMode = TransferMode.Full;
                }
            }
            else if (task is UnloadTask)
            {
                if (transferOption == FullTransferOption.NoWait)
                {
                    task.UnloadMode = TransferMode.Partial;
                }
                else
                {
                    task.UnloadMode = TransferMode.Full;
                }
            }
        }

        [CanBeNull]
        private TransferTaskInfo GetTransferTaskInfo(TransferTask task)
        {
            if (!_cachedTaskInfo.TryGetValue(task, out TransferTaskInfo taskInfo))
            {
                if (_tasksData.TryGetValue(task, out TransferTaskData data))
                {
                    taskInfo = new TransferTaskInfo(task, data.Percent ?? (task is LoadTask ? 100 : 0));
                }

                _cachedTaskInfo[task] = taskInfo;
            }

            return taskInfo;
        }

        private int GetSumOfLoadedItems([NotNullAttribute] TransferTask task,[NotNullAttribute] Item item)
        {
            Dictionary<Item, int> items = GetSumOfLoadedItems(task);
            if (items.TryGetValue(item, out int count))
            {
                return count;
            }

            return 0;
        } 

        private Dictionary<Item, int> GetSumOfLoadedItems([NotNullAttribute] TransferTask task)
        {
            if (!_cachedLoadedItems.TryGetValue(task, out Dictionary<Item, int> result))
            {
                result = new Dictionary<Item, int>();
                _cachedLoadedItems[task] = result;
            }

            if (result.Count == 0)
            {
                ImmutableUniqueList<VehicleUnit> units = task.GetTargetUnits();
                for (int i = 0; i < units.Count; i++)
                {
                    VehicleUnit unit = units[i];
                    if (unit.Storage != null)
                    {
                        result.AddIntToDict(unit.Storage.Item, unit.Storage.Count);
                    }
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
            TransferTaskData taskData = _tasksData.GetValueOrDefault(task, null);
            if (taskInfo != null && taskData != null)
            {
                Dictionary<Item, int> loadedItems = GetSumOfLoadedItems(task);
                foreach (KeyValuePair<Item, int> capacity in taskInfo.GetCapacityPerItem())
                {
                    if (loadedItems.TryGetValue(capacity.Key, out int loaded) && loaded > capacity.Value)
                    {
                        if (taskData.FullAny == false)
                        {
                            return false;
                        }
                    }
                    else if (taskData.FullAny)
                    {
                        return true;
                    }
                }

                if (taskData.FullAny)
                {
                    return false;
                }
            }

            return true;
        }
        
        private bool IsLoadingComplete(TransferTask task)
        {
            TransferTaskInfo taskInfo = GetTransferTaskInfo(task);
            TransferTaskData taskData = _tasksData.GetValueOrDefault(task, null);
            if (taskInfo != null && taskData != null)
            {
                Dictionary<Item, int> loadedItems = GetSumOfLoadedItems(task);
                foreach (KeyValuePair<Item, int> capacity in taskInfo.GetCapacityPerItem())
                {
                    if (!loadedItems.TryGetValue(capacity.Key, out int loaded) || loaded < capacity.Value)
                    {
                        if (taskData.FullAny == false)
                        {
                            return false;
                        }
                    }
                    else if (taskData.FullAny)
                    {
                        return true;
                    }
                }

                if (taskData.FullAny)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnTaskStart(TransferTask task)
        {
            if (_tasksData.ContainsKey(task))
            {
                RemoveCachedValues(task);
                ImmutableUniqueList<VehicleUnit> units = task.GetTargetUnits();
                for (int i = 0; i < units.Count; i++)
                {
                    VehicleUnit unit = units[i];
                    unit.StorageChanged -= OnStorageChanged;
                    unit.StorageChanged += OnStorageChanged;
                    if (unit.Storage != null)
                    {
                        unit.Storage.CountChanged -= OnStorageCountChanged;
                        unit.Storage.CountChanged += OnStorageCountChanged;
                        _storageToUnit.Remove(unit.Storage);
                        _storageToUnit.Add(unit.Storage, unit);
                    }
                }                
            }
        }

        private void OnTaskStop(TransferTask task)
        {
            if (_tasksData.ContainsKey(task))
            {
                RemoveCachedValues(task);
                ImmutableUniqueList<VehicleUnit> units = task.GetTargetUnits();
                for (int i = 0; i < units.Count; i++)
                {
                    VehicleUnit unit = units[i];
                    unit.StorageChanged -= OnStorageChanged;
                    if (unit.Storage != null)
                    {
                        unit.Storage.CountChanged -= OnStorageCountChanged;
                        _storageToUnit.Remove(unit.Storage);
                    }
                }                
            }
        }

        [CanBeNull]
        private TransferTask GetActualTransferTask(VehicleUnit unit)
        {
            RootTask rootTask = unit.Vehicle.Schedule.CurrentTask as RootTask;
            return rootTask?.CurrentSubTask as TransferTask;
        }

        private void OnStorageChanged(object obj, Storage oldStorage, Storage newStorage)
        {
            if (oldStorage != null)
            {
                _storageToUnit.Remove(oldStorage);
            }
            if (obj is VehicleUnit unit)
            {
                TransferTask transferTask = GetActualTransferTask(unit);
                if (transferTask != null)
                {
                    RemoveCachedValues(transferTask);
                    if (newStorage != null)
                    {
                        _storageToUnit.Add(newStorage, unit);
                    }
                }
            }
        }

        private void OnStorageCountChanged(Storage storage, int oldValue, int newValue)
        {
            if (_storageToUnit.TryGetValue(storage, out VehicleUnit unit))
            {
                TransferTask transferTask = GetActualTransferTask(unit);
                if (transferTask != null && _cachedLoadedItems.TryGetValue(transferTask, out Dictionary<Item, int> loadedItems))
                {
                    if (loadedItems.TryGetValue(storage.Item, out int value))
                    {
                        loadedItems[storage.Item] = value - oldValue + newValue;
                    }
                    else
                    {
                        //inconsistency, we clear the cache
                        loadedItems.Clear();
                    }
                }
            }
        }

        private void OnTaskRemoved(TransferTask task)
        {
            OnTaskStop(task);
        }

        internal void Write(StateBinaryWriter writer)
        {
            writer.WriteInt(_tasksData.Count);
            foreach (KeyValuePair<TransferTask, TransferTaskData> taskData in _tasksData)
            {
                Vehicle vehicle = taskData.Key.Vehicle;
                int rootTaskIndex = -1;
                int taskIndex = -1;
                if (vehicle != null)
                {
                    rootTaskIndex = taskData.Key.ParentTask.GetIndex();
                    taskIndex = taskData.Key.GetIndex();
                }

                if (vehicle != null && rootTaskIndex != -1 && taskIndex != -1)
                {
                    writer.WriteInt(vehicle.Id);
                    writer.WriteInt(rootTaskIndex);
                    writer.WriteInt(taskIndex);
                    writer.WriteInt(taskData.Value.Percent.GetValueOrDefault(-1));
                    writer.WriteBool(taskData.Value.FullAny); //new in version 2
                }
                else
                {
                    writer.WriteInt(-1);
                }
            }
        }

        internal void Read(StateBinaryReader reader)
        {
            _tasksData.Clear();
            int count = reader.ReadInt();
            int version = SchemaVersion<TransferTasksManager>.Get();
            for (int i = 0; i < count; i++)
            {
                int vehicleIndex = reader.ReadInt();
                if (vehicleIndex <= -1) continue;
                
                Vehicle vehicle = LazyManager<VehicleManager>.Current.FindById(vehicleIndex);
                if (vehicle == null) continue;

                int rootTaskIndex = reader.ReadInt();
                int taskIndex = reader.ReadInt();
                int percent = reader.ReadInt();
                
                bool fullAny = version >= 2 && reader.ReadBool();

                try
                {
                    SubTask subTask = vehicle.Schedule?.GetSubTask(rootTaskIndex, taskIndex);
                    if (subTask is TransferTask transferTask)
                    {
                        TransferTaskData data = new(){ Percent = percent == -1 ? null : percent, FullAny = fullAny};

                        if (!data.IsDefault)
                        {
                            _tasksData[transferTask] = data;
                        }
                    }
                }
                catch
                {
                    Logger.Log(LogType.Error, "Error getting subtask while loading.");
                }
            }
        }

        private void InvalidateLoadedItems(TransferTask task)
        {
            if (_cachedLoadedItems.TryGetValue(task, out Dictionary<Item, int> loadedItems))
            {
                loadedItems.Clear();;
            }
        }
        
        private void RemoveCachedValues(TransferTask task)
        {
            _cachedTaskInfo.Remove(task);
            InvalidateLoadedItems(task);
        }

        private void CopyFrom(TransferTask task, TransferTask original)
        {
            if (_tasksData.TryGetValue(original, out TransferTaskData taskData))
            {
                _tasksData[task] = taskData;
            }
        }
        
        #region HARMONY

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VehicleTask), "OnRemove")]
        private static void VehicleTask_OnRemove_pof(VehicleTask __instance)
        {
            if (__instance is TransferTask transferTask)
            {
                Current.OnTaskRemoved(transferTask);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnitsTask), "OnStart")]
        private static void UnitsTask_OnStart_pof(UnitsTask __instance)
        {
            if (__instance is TransferTask transferTask)
            {
                Current.OnTaskStart(transferTask);
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
            if (Current._tasksData.TryGetValue(__instance, out TransferTaskData taskData))
            {
                __result = __result * -0x5AAAAAD7 + taskData.GetHashCode();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TransferTask), "CopyFrom")]
        private static void TransferTask_CopyFrom_pof(TransferTask __instance, SubTask original)
        {
            if (original is TransferTask originalTask)
            {
                Current.CopyFrom(__instance, originalTask);
            }
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TransferTask), "IsUnloading")]
        private static void TransferTask_IsUnloading_prf(TransferTask __instance, out bool __state)
        {
            __state = false;
            if (__instance.UnloadMode == TransferMode.Full && Current._tasksData.TryGetValue(__instance, out TransferTaskData data) && !data.IsDefault)
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
            if (__instance.LoadMode == TransferMode.Full && Current._tasksData.TryGetValue(__instance, out TransferTaskData data) && !data.IsDefault)
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