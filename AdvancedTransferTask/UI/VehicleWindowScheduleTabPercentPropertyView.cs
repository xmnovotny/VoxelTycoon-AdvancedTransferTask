using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using VoxelTycoon;
using VoxelTycoon.Game.UI;
using VoxelTycoon.Tracks.Tasks;
using VoxelTycoon.UI.Controls;
using VoxelTycoon.UI.Windows;

namespace AdvancedTransferTask.UI
{
    [HarmonyPatch]
    public class VehicleWindowScheduleTabPercentPropertyView: VehicleWindowScheduleTabSubTaskPropertyView<TransferTask>
    {
        private static VehicleWindowScheduleTabPercentPropertyView _template;
        private TransferTask _task;
        private Text _text;
        
        public override bool Initialize([NotNullAttribute] TransferTask task, bool editMode)
        {
            int? percent = LazyManager<TransferTasksManager>.Current.GetTaskPercent(task);
            if (!editMode && percent == null)
            {
                return false;
            }
            _task = task;
            _text = base.transform.Find<Text>("Text");
            Button component = GetComponent<Button>();
            if (editMode)
            {
                if (percent == null)
                {
                    percent = 100;
                }
                base.gameObject.AddComponent<ClickableDecorator>();
                component.onClick.AddListener(OnClick);
            }
            else
            {
                component.interactable = false;
            }

            _text.text = $"{percent}%";
            return true;
        }

        private void OnClick()
        {
            TransferTasksManager manager = LazyManager<TransferTasksManager>.Current;
            int? percent = manager.GetTaskPercent(_task);
            InputDialog.ShowFor("Set percent of loading/unloading", percent != null ? percent.Value.ToString() : "", InputField.CharacterValidation.Integer, delegate(string s)
            {
                FileLog.Log($"Clicked: {s}");
                int? newPercent;
                if (s.Trim() == "")
                {
                    newPercent = null;
                } else
                if (int.TryParse(s, out var value))
                {
                    newPercent = Mathf.RoundToInt(Mathf.Clamp(value, 1f, 100f));
                    if (newPercent == 100)
                    {
                        newPercent = null;
                    }
                }
                else
                {
                    return;
                }
                
                FileLog.Log($"NewPercent: {newPercent}");
                RouteHelper.PropagateAction(_task, delegate(TransferTask t)
                {
                    manager.SetTaskPercent(t, newPercent);
                });
            });
        }

        private static VehicleWindowScheduleTabPercentPropertyView GetInstance(Transform parent)
        {
            return Instantiate(GetTemplate(), parent);
        }

        private static VehicleWindowScheduleTabPercentPropertyView GetTemplate()
        {
            if (_template == null)
            {
                VehicleWindowScheduleTabDelayPropertyView comp = Instantiate(R.Game.UI.VehicleWindow.ScheduleTab.VehicleWindowScheduleTabDelayPropertyView);
                Transform transf = comp.transform;
                DestroyImmediate(comp);
                DestroyImmediate(transf.Find("Icon").gameObject);
                
                _template = transf.gameObject.AddComponent<VehicleWindowScheduleTabPercentPropertyView>();
            }

            return _template;
        }

        #region HARMONY

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VehicleWindowScheduleTabSubTaskView), "Initialize")]
        private static void VehicleWindowScheduleTabSubTaskView_Initialize_pof(VehicleWindowScheduleTabSubTaskView __instance)
        {
            if (__instance.Task is TransferTask transferTask)
            {
                VehicleWindowScheduleTabPercentPropertyView percView = GetInstance(null);
                if (percView.Initialize(transferTask, __instance.ScheduleTab.EditMode))
                {
                    percView.transform.SetParent(__instance.transform.Find("Content/Properties"), false);
                }
                else
                {
                    Destroy(percView.gameObject);
                }
            }
        }


        #endregion        
    }
}