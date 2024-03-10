using Pipliz;
using Pipliz.Collections;
using BlockTypes;
using Saving;
using BlockEntities;


namespace AntiFlood
{
    [ModLoader.ModManager]
    [BlockEntityAutoLoader]
    public class WaterAntiFlood : ISingleBlockEntityMapping, IUpdatedAdjacentType, IChangedWithType
    {
        public static System.Collections.Generic.Dictionary<ColonyID, Players.PlayerID> coloniesWithWaterEnabled = new System.Collections.Generic.Dictionary<ColonyID, Players.PlayerID>();

        private static ServerTimeStamp NextTick;

        private static int UpdatesPerTick;

        private static long MillisecondsPerTick;

        public static SortedSet<Vector3Int> locationsToCheck = new SortedSet<Vector3Int>(10, (Vector3Int a, Vector3Int b) => a.CompareTo(b));

        private static SortedSet<Vector3Int> tempList = new SortedSet<Vector3Int>(10, (Vector3Int a, Vector3Int b) => a.CompareTo(b));

        ItemTypes.ItemType ISingleBlockEntityMapping.TypeToRegister => BuiltinBlocks.Types.water;

        void IUpdatedAdjacentType.OnUpdateAdjacent(AdjacentUpdateData data)
        {
            if (ServerManager.ServerSettings.Water.MaxUpdatesPerTick > 0)
            {
                locationsToCheck.AddIfUnique(data.UpdatePosition);
            }
        }

        void IChangedWithType.OnChangedWithType(Chunk chunk, BlockChangeRequestOrigin requestOrigin, Vector3Int blockPosition, ItemTypes.ItemType typeOld, ItemTypes.ItemType typeNew)
        {
            if (typeNew == BuiltinBlocks.Types.water && ServerManager.ServerSettings.Water.MaxUpdatesPerTick > 0)
            {
                locationsToCheck.AddIfUnique(blockPosition);
            }
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnUpdate, "Khanx.AntiFlood.update_water")]
        private static void Tick()
        {
            if (UpdatesPerTick > 0 && NextTick.IsPassed)
            {
                NextTick = NextTick.Add(MillisecondsPerTick);
                ProcessTick();
            }
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "Khanx.AntiFlood.endloadwater")]
        private static void Load()
        {
            UpdatesPerTick = ServerManager.ServerSettings.Water.MaxUpdatesPerTick;
            MillisecondsPerTick = ServerManager.ServerSettings.Water.TickTimeMilliSeconds;
            NextTick = ServerTimeStamp.Now.Add(MillisecondsPerTick);
            if (UpdatesPerTick <= 0)
            {
                return;
            }

            foreach (Vector3Int item in ServerManager.SaveManager.WorldDataBase.ExtractWaterSpread())
            {
                locationsToCheck.Add(item);
            }
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAutoSaveWorld, "Khanx.AntiFlood.autosavewater")]
        [ModLoader.ModDocumentation("Saves water data")]
        private static void Save()
        {
            WorldDB worldDataBase = ServerManager.SaveManager.WorldDataBase;
            if (worldDataBase != null)
            {
                if (locationsToCheck.Count <= 0)
                {
                    worldDataBase.SetWaterSpread(null, 0);
                }
                else
                {
                    worldDataBase.SetWaterSpread(locationsToCheck.EntriesRaw, locationsToCheck.Count);
                }
            }
        }

        private static void ProcessTick()
        {
            int spreadMax = UpdatesPerTick;
            if (locationsToCheck.Count <= 0)
            {
                return;
            }

            if (spreadMax == 0)
            {
                locationsToCheck.Clear();
                return;
            }

            locationsToCheck.CopyTo(tempList);
            int count = tempList.Count;
            int num = count - 1;
            while (num >= 0 && spreadMax > 0)
            {
                CheckSpreadabilityToNeighbours(tempList[num], ref spreadMax);
                num--;
            }
        }

        private static void CheckSpreadabilityToNeighbours(Vector3Int position, ref int spreadMax)
        {
            if (spreadMax <= 0 || !World.TryGetTypeAt(position, out ItemTypes.ItemType val))
            {
                return;
            }

            if (val == BuiltinBlocks.Types.water)
            {
                if (CheckSpreadabilitySpot(position.Add(-1, 0, 0), ref spreadMax) && CheckSpreadabilitySpot(position.Add(1, 0, 0), ref spreadMax) && CheckSpreadabilitySpot(position.Add(0, 0, -1), ref spreadMax) && CheckSpreadabilitySpot(position.Add(0, 0, 1), ref spreadMax) && CheckSpreadabilitySpot(position.Add(0, -1, 0), ref spreadMax))
                {
                    locationsToCheck.Remove(position);
                }
            }
            else
            {
                locationsToCheck.Remove(position);
            }
        }

        private static bool CheckSpreadabilitySpot(Vector3Int spot, ref int spreadMax)
        {
            //Search CLOSEST banner | Not sure if this is the best way... Water won't spread through abandoned banners...
            if (ServerManager.BlockEntityTracker.BannerTracker.TryGetClosest(spot, out var banner))
            {
                //Check SAFE area
                if ((spot - banner.Position).MaxPartAbs <= banner.SafeRadius)
                {
                    //Water spread disabled
                    if (!coloniesWithWaterEnabled.ContainsKey(banner.Colony.ColonyGroup.MainColonyID))
                        return true;
                }
            }

            switch (ServerManager.TryChangeBlock(spot, BuiltinBlocks.Types.air, BuiltinBlocks.Types.water, default(BlockChangeRequestOrigin), ESetBlockFlags.TriggerEntityCallbacks | ESetBlockFlags.TriggerNeighbourCallbacks))
            {
                case EServerChangeBlockResult.Success:
                    spreadMax--;
                    return true;
                case EServerChangeBlockResult.ChunkNotReady:
                    return false;
                default:
                    return true;
            }
        }
    }
}
