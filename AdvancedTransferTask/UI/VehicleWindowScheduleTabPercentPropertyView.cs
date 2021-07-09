using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using VoxelTycoon;
using VoxelTycoon.Game.UI;
using VoxelTycoon.Localization;
using VoxelTycoon.Tracks.Tasks;
using VoxelTycoon.UI;
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
                if (percent == null && _task is LoadTask)
                {
                    percent = 100;
                }
                else if (percent == null && _task is UnloadTask)
                {
                    percent = 0;
                }
                base.gameObject.AddComponent<ClickableDecorator>();
                component.onClick.AddListener(OnClick);

                Tooltip.For(_text.transform.parent, LazyManager<LocaleManager>.Current.Locale.GetString("advanced_transfer_task/edit_tooltip"));
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
            Locale locale = LazyManager<LocaleManager>.Current.Locale;
            InputDialog.ShowFor(_task is LoadTask ? locale.GetString("advanced_transfer_task/percentage_loaded") : locale.GetString("advanced_transfer_task/percentage_unloaded"), percent != null ? percent.Value.ToString() : "", InputField.CharacterValidation.Integer, delegate(string s)
            {
                int? newPercent;
                if (s.Trim() == "")
                {
                    newPercent = null;
                } else
                if (int.TryParse(s, out var value))
                {
                    newPercent = Mathf.Clamp(value, 0, 100);
                    if ((newPercent == 100 && _task is LoadTask) || (newPercent == 0 && _task is UnloadTask))
                    {
                        newPercent = null;
                    }
                    else
                    {
                        newPercent = Mathf.Clamp(newPercent.Value, 1, 99);
                    }
                }
                else
                {
                    return;
                }
                
                RouteHelper.PropagateAction(_task, delegate(TransferTask t)
                {
                    manager.SetTaskPercent(t, newPercent);
                });
            });
        }

        internal static VehicleWindowScheduleTabPercentPropertyView GetInstance(Transform parent)
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
    }
}