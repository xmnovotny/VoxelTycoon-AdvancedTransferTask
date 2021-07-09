using System;
using UnityEngine;
using UnityEngine.UI;
using VoxelTycoon;
using VoxelTycoon.Game.UI;
using VoxelTycoon.Localization;
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
			Text text = transform.Find<Text>("Text");
			text.text = 
				(_transferOption == FullTransferOption.FullAny ? LazyManager<LocaleManager>.Current.Locale.GetString("advanced_transfer_task/full_any") : S.VehicleWindowScheduleFull.ToUpper()).ToUpper();
			text.horizontalOverflow = HorizontalWrapMode.Overflow;
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
			Locale locale = LazyManager<LocaleManager>.Current.Locale;
			string dir = _task is LoadTask ? locale.GetString("advanced_transfer_task/load") : locale.GetString("advanced_transfer_task/unload");
			menu.AddItem(locale.GetString("advanced_transfer_task/menu_not_wait").Format(dir), () => SetTransferOption(FullTransferOption.NoWait));
			menu.AddItem(locale.GetString("advanced_transfer_task/menu_wait_for_any").Format(dir), () => SetTransferOption(FullTransferOption.FullAny));
			menu.AddItem(locale.GetString("advanced_transfer_task/menu_wait_for_all").Format(dir), () => SetTransferOption(FullTransferOption.FullAll));
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
			Locale locale = LazyManager<LocaleManager>.Current.Locale;
			string dir = _task is LoadTask ? locale.GetString("advanced_transfer_task/load") : locale.GetString("advanced_transfer_task/unload");
			switch (_transferOption)
			{
				case FullTransferOption.NoWait:
					return locale.GetString("advanced_transfer_task/tooltip_not_wait").Format(dir);
				case FullTransferOption.FullAny:
					return locale.GetString("advanced_transfer_task/tooltip_wait_for_any").Format(dir);
				case FullTransferOption.FullAll:
					return locale.GetString("advanced_transfer_task/tooltip_wait_for_all").Format(dir);
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