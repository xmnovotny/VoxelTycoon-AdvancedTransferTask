using System;
using HarmonyLib;
using VoxelTycoon;
using VoxelTycoon.Localization;
using VoxelTycoon.Modding;
using VoxelTycoon.Serialization;

namespace AdvancedTransferTask
{
    [SchemaVersion(1)]
    public class AdvancedTransferTaskMod: Mod
    {
        private Harmony _harmony;
        private const string _harmonyID = "cz.xmnovotny.advancedtransfertask.patch";
        public static readonly Logger Logger = new Logger("AdvancedTransferTask");

        protected override void Initialize()
        {
            Harmony.DEBUG = false;
            _harmony = (Harmony) (object) new Harmony(_harmonyID);
            FileLog.Reset();
            _harmony.PatchAll();
        }

        protected override void OnGameStarted()
        {
/*            ModSettingsWindowManager.Current.Register<SettingsWindowPage>(this.GetType().Name,
                LazyManager<LocaleManager>.Current.Locale.GetString("schedule_stopwatch/settings_window_title"));*/
        }

        protected override void Deinitialize()
        {
            _harmony.UnpatchAll(_harmonyID);
            _harmony = null;
        }

        protected override void Write(StateBinaryWriter writer)
        {
            LazyManager<TransferTasksManager>.Current.Write(writer);
        }

        protected override void Read(StateBinaryReader reader)
        {
            int version = SchemaVersion<AdvancedTransferTaskMod>.Get();
            if (version > 0)
            {
                LazyManager<TransferTasksManager>.Current.Read(reader);
            }
        }
    }
}