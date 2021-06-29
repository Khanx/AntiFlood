using BlockEntities;
using BlockTypes;
using Pipliz;
using Pipliz.Helpers;
using Pipliz.JSON;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AntiFlood
{
    [ModLoader.ModManager]
    [BlockEntityAutoLoader]
    public class WaterAntiFlood : ISingleBlockEntityMapping, IUpdatedAdjacentType, IChangedWithType
    {
        public static Dictionary<int, NetworkID> coloniesWithWaterEnabled = new Dictionary<int, NetworkID>();

        private static readonly Stopwatch tickTimer = Stopwatch.StartNew();

        private static readonly Pipliz.Collections.SortedList<Vector3Int, bool> locationsToCheck = new Pipliz.Collections.SortedList<Vector3Int, bool>(10, (Vector3Int a, Vector3Int b) => a.CompareTo(b));

        private static readonly Pipliz.Collections.SortedList<Vector3Int, bool> tempList = new Pipliz.Collections.SortedList<Vector3Int, bool>(10, (Vector3Int a, Vector3Int b) => a.CompareTo(b));

        private static Task<JSONNode> LoadingTask;

        ItemTypes.ItemType ISingleBlockEntityMapping.TypeToRegister => BuiltinBlocks.Types.water;

        void IUpdatedAdjacentType.OnUpdateAdjacent(AdjacentUpdateData data)
        {
            if (ServerManager.ServerSettings.Water.MaxUpdatesPerTick > 0)
            {
                locationsToCheck.AddIfUnique(data.UpdatePosition, val: true);
            }
        }

        void IChangedWithType.OnChangedWithType(Chunk chunk, BlockChangeRequestOrigin requestOrigin, Vector3Int blockPosition, ItemTypes.ItemType typeOld, ItemTypes.ItemType typeNew)
        {
            if (typeNew == BuiltinBlocks.Types.water && ServerManager.ServerSettings.Water.MaxUpdatesPerTick > 0)
            {
                locationsToCheck.AddIfUnique(blockPosition, val: true);
            }
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnUpdate, "Khanx.AntiFlood.update_water")]
        private static void Tick()
        {
            if (ServerManager.ServerSettings.Water.MaxUpdatesPerTick > 0)
            {
                if (!tickTimer.IsRunning)
                {
                    tickTimer.Start();
                }

                if (tickTimer.ElapsedMilliseconds >= ServerManager.ServerSettings.Water.TickTimeMilliSeconds)
                {
                    tickTimer.Reset();
                    ProcessTick();
                    tickTimer.Start();
                }
            }
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterSelectedWorld, "Khanx.AntiFlood.startloadwater", -100f)]
        private static void LoadStart()
        {
            LoadingTask = Task.Run(() => JSON.Deserialize("gamedata/savegames/" + ServerManager.WorldName + "/blocktypes/water.json", errorIfMissing: false));
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, "Khanx.AntiFlood.endloadwater")]
        [ModLoader.ModDocumentation("Starts loading water blocks")]
        private static void Load()
        {
            LoadingTask.Wait();
            JSONNode result = LoadingTask.Result;
            LoadingTask.Dispose();
            LoadingTask = null;
            if (result == null || ServerManager.ServerSettings.Water.MaxUpdatesPerTick <= 0)
            {
                return;
            }

            foreach (JSONNode item in result["NodesToCheck"].LoopArray())
            {
                try
                {
                    locationsToCheck.Add((Vector3Int)item, val: true);
                }
                catch (Exception exc)
                {
                    Log.WriteException("Error loading a water node to check;", exc);
                }
            }
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnQuit, "Khanx.AntiFlood.savewater")]
        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAutoSaveWorld, "Khanx.AntiFlood.autosavewater")]
        [ModLoader.ModDocumentation("Saves water data")]
        private static void Save()
        {
            if (ServerManager.WorldName == null)
            {
                return;
            }

            string filePath = $"gamedata/savegames/{ServerManager.WorldName}/blocktypes/water.json";
            if (locationsToCheck.Count > 0)
            {
                JSONNode root = new JSONNode();
                JSONNode jSONNode = new JSONNode(NodeType.Array);
                jSONNode.SetArrayCapacity(locationsToCheck.Count);
                root["NodesToCheck"] = jSONNode;
                for (int i = 0; i < locationsToCheck.Count; i++)
                {
                    jSONNode.AddToArray((JSONNode)locationsToCheck.GetKeyAtIndex(i));
                }

                Application.StartAsyncQuitToComplete(delegate
                {
                    IOHelper.CreateDirectoryFromFile(filePath);
                    JSON.Serialize(filePath, root);
                });
                return;
            }

            Application.StartAsyncQuitToComplete(delegate
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            });
        }

        private static void ProcessTick()
        {
            int spreadMax = ServerManager.ServerSettings.Water.MaxUpdatesPerTick;
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
            int num = tempList.Count - 1;
            while (num >= 0 && spreadMax > 0)
            {
                CheckSpreadabilityToNeighbours(tempList.GetKeyAtIndex(num), ref spreadMax);
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
            if (ServerManager.BlockEntityTracker.BannerTracker.IsSafeZone(spot, out Vector3Int foundBanner))
            {
                ServerManager.BlockEntityTracker.BannerTracker.TryGetAt(foundBanner, out BlockEntities.Implementations.BannerTracker.Banner banner);

                if (!coloniesWithWaterEnabled.ContainsKey(banner.Colony.ColonyID))
                    return true;
            }

            switch (ServerManager.TryChangeBlock(spot, BuiltinBlocks.Types.air, BuiltinBlocks.Types.water, default, ESetBlockFlags.TriggerEntityCallbacks | ESetBlockFlags.TriggerNeighbourCallbacks))
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
