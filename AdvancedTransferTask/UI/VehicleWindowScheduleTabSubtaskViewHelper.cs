using System;
using HarmonyLib;
using UnityEngine;
using VoxelTycoon.Game.UI;
using VoxelTycoon.Tracks.Tasks;
using Object = UnityEngine.Object;

namespace AdvancedTransferTask.UI
{
    [HarmonyPatch]
    public static class VehicleWindowScheduleTabSubtaskViewHelper
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(VehicleWindowScheduleTabFullPropertyView), "Initialize")]
        private static bool VehicleWindowScheduleTabFullPropertyView_Initialize_prf(VehicleWindowScheduleTabFullPropertyView __instance, ref bool __result)
        {
            __result = true;
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VehicleWindowScheduleTabSubTaskView), "Initialize")]
        private static void VehicleWindowScheduleTabSubTaskView_Initialize_pof(VehicleWindowScheduleTabSubTaskView __instance, VehicleWindowScheduleTab ____scheduleTab)
        {
            if (__instance.Task is TransferTask transferTask)
            {
                VehicleWindowScheduleTabPercentPropertyView percView = VehicleWindowScheduleTabPercentPropertyView.GetInstance(null);
                if (percView.Initialize(transferTask, __instance.ScheduleTab.EditMode))
                {
                    percView.transform.SetParent(__instance.transform.Find("Content/Properties"), false);
                }
                else
                {
                    Object.Destroy(percView.gameObject);
                }

                VehicleWindowScheduleTabFullPropertyView fullView =  __instance.gameObject.GetComponentInChildren<VehicleWindowScheduleTabFullPropertyView>();
                if (fullView != null)
                {
                    Transform fullViewTr = fullView.transform;
                    Object.DestroyImmediate(fullView);
                    if (!fullViewTr.gameObject.AddComponent<VehicleWindowScheduleTabAdvancedFullPropertyView>()
                        .Initialize(transferTask, ____scheduleTab.EditMode))
                    {
                        Object.Destroy(fullViewTr.gameObject);
                    }
                }
            }
        }
    }
}