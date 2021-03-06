using System;
using System.Reflection;
using AdvancedTransferTask.UI;
using HarmonyLib;
using VoxelTycoon;
using VoxelTycoon.Game.UI;
using VoxelTycoon.Localization;
using VoxelTycoon.Modding;
using VoxelTycoon.Serialization;
using VoxelTycoon.Tracks.Tasks;

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