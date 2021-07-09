using System;
using UnityEngine;
using UnityEngine.UI;
using VoxelTycoon;
using VoxelTycoon.Game.UI;
using VoxelTycoon.Tracks.Tasks;
using VoxelTycoon.UI;
using VoxelTycoon.UI.Controls;

namespace AdvancedTransferTask.UI
{
    public class VehicleWindowScheduleTabAdvancedFullPropertyView : VehicleWindowScheduleTabSubTaskPropertyView<TransferTask>
    {
		private bool _editMode;

		private Text _icon;

		private TransferTask _task;

		private FullTransferOption _transferOption;

		public override bool Initialize(TransferTask task, bool editMode)
		{
			_task = task ?? throw new ArgumentNullException(nameof(task));
			_transferOption = LazyManager<TransferTasksManager>.Current.GetTaskTransferOption(task);
			if (!editMode && _transferOption == FullTransferOption.NoWait)
			{
				return false;
			}
			_editMode = editMode;
			_icon = transform.Find<Text>("Icon");
			transform.Find<Text>("Text").text =
				(_transferOption == FullTransferOption.FullAny ? "Full any" : "Full").ToUpper();
			Button component = GetComponent<Button>();
			_icon.SetFontIcon(_transferOption != FullTransferOption.NoWait ? FontIcon.FaSolid("\uf14a") : FontIcon.FaRegular("\uf0c8"));
			if (editMode)
			{
				VoxelTycoon.UI.ContextMenu.For(component, PickerBehavior.OverlayToRight, SetupContextMenu);
				gameObject.AddComponent<ClickableDecorator>();
			}
			else
			{
				component.interactable = false;
				_icon.color = Color.black.WithAlpha(0.5f);
			}
			Tooltip.For(this, GetTooltip());
			Update();
			return true;
		}
		private void SetupContextMenu(VoxelTycoon.UI.ContextMenu menu)
		{
			string dir = _task is LoadTask ? "load" : "unload";
			menu.AddItem("Do not wait for full {0}".Format(dir), () => SetTransferOption(FullTransferOption.NoWait));
			menu.AddItem("Wait for full {0} of any item type".Format(dir), () => SetTransferOption(FullTransferOption.FullAny));
			menu.AddItem("Wait for full {0} of all items".Format(dir), () => SetTransferOption(FullTransferOption.FullAll));
		}

		private void SetTransferOption(FullTransferOption option)
		{
			TransferTasksManager manager = LazyManager<TransferTasksManager>.Current;
			RouteHelper.PropagateAction(_task, delegate(TransferTask t)
			{
				manager.SetTaskTransferOption(t, option);
			});
		}
		
		private string GetTooltip()
		{
			string dir = _task is LoadTask ? "load" : "unload";
			switch (_transferOption)
			{
				case FullTransferOption.NoWait:
					return "Will not wait for full {0}".Format(dir);
				case FullTransferOption.FullAny:
					return "Will wait for full {0} of any item type".Format(dir);
				case FullTransferOption.FullAll:
					return "Will wait for full {0} of all items".Format(dir);
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void Update()
		{
			if (_editMode)
			{
				_icon.color = Company.Current.Color;
			}
		}
        
    }
}