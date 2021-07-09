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

		public override bool Initialize(TransferTask task, bool editMode)
		{
			if (task == null)
			{
				throw new ArgumentNullException("task");
			}
			_task = task;
			if (!editMode && !GetValue())
			{
				return false;
			}
			_editMode = editMode;
			_icon = base.transform.Find<Text>("Icon");
			base.transform.Find<Text>("Text").text = "Fulload";// S.VehicleWindowScheduleFull.ToUpper();
			Button component = GetComponent<Button>();
			if (editMode)
			{
				VoxelTycoon.UI.ContextMenu.For(component, PickerBehavior.OverlayToRight, new Action<VoxelTycoon.UI.ContextMenu>(this.SetupContextMenu));
				base.gameObject.AddComponent<ClickableDecorator>();
				_icon.SetFontIcon(GetValue() ? FontIcon.FaSolid("\uf14a") : FontIcon.FaRegular("\uf0c8"));
			}
			else
			{
				component.interactable = false;
				_icon.color = Color.black.WithAlpha(0.5f);
				_icon.SetFontIcon(FontIcon.FaRegular("\uf14a"));
			}
			Tooltip.For(this, GetTooltip());
			Update();
			return true;
		}
		private void SetupContextMenu(VoxelTycoon.UI.ContextMenu menu)
		{
			menu.AddItem("Do not wait", () => SetTransferOption(FullTransferOption.NoWait));
			menu.AddItem("Wait for full of any item", () => SetTransferOption(FullTransferOption.FullAny));
			menu.AddItem("Wait for full of all items", () => SetTransferOption(FullTransferOption.FullAll));
		}

		private void SetTransferOption(FullTransferOption option)
		{
			
		}
		
		private DisplayString GetTooltip()
		{
			TransferTask task = _task;
			if (!(task is UnloadTask))
			{
				if (task is LoadTask)
				{
					return S.VehicleWindowScheduleWaitForFullLoad;
				}
				throw new ArgumentException();
			}
			return S.VehicleWindowScheduleWaitForFullUnload;
		}

		private bool GetValue()
		{
			TransferTask task = _task;
			if (!(task is UnloadTask))
			{
				if (task is LoadTask)
				{
					return _task.LoadMode == TransferMode.Full;
				}
				throw new ArgumentException();
			}
			return _task.UnloadMode == TransferMode.Full;
		}

		private void OnClick()
		{
			TransferTask task = _task;
			if (!(task is UnloadTask))
			{
				if (!(task is LoadTask))
				{
					throw new ArgumentException();
				}
				TransferMode loadMode = ((_task.LoadMode == TransferMode.Full) ? TransferMode.Partial : TransferMode.Full);
				RouteHelper.PropagateAction(_task, delegate(TransferTask t)
				{
					t.LoadMode = loadMode;
				});
			}
			else
			{
				TransferMode unloadMode = ((_task.UnloadMode == TransferMode.Full) ? TransferMode.Partial : TransferMode.Full);
				RouteHelper.PropagateAction(_task, delegate(TransferTask t)
				{
					t.UnloadMode = unloadMode;
				});
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