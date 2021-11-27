﻿using ModLoaderInterfaces;
using NetworkUI;
using NetworkUI.Items;
using Pipliz;
using System.Collections.Generic;

namespace AntiFlood
{
    [ModLoader.ModManager]
    public class AntiFloodInterface : IOnConstructInventoryManageColonyUI
    {
        public void OnConstructInventoryManageColonyUI(Players.Player player, NetworkMenu menu, (Table left, Table right) tables)
        {
            if (player.ActiveColony == null)
                return;

            //Only the leader of the colony can enable / disable water spread
            if (player.ActiveColony.Owners[0] != player)
                return;

            ButtonCallback spreadWater;

            if (!WaterAntiFlood.coloniesWithWaterEnabled.ContainsKey(player.ActiveColony.ColonyID))
                spreadWater = new ButtonCallback("Khanx.AntiFlood.Button", new LabelData("Enable water spread", UnityEngine.Color.green, UnityEngine.TextAnchor.MiddleCenter));
            else
                spreadWater = new ButtonCallback("Khanx.AntiFlood.Button", new LabelData("Disable water spread", UnityEngine.Color.red, UnityEngine.TextAnchor.MiddleCenter));

            tables.left.Rows.Add(spreadWater);
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerPushedNetworkUIButton, "Khanx.AntiFlood.OnPlayerPushedNetworkUIButton")]
        public static void OnPlayerPushedNetworkUIButton(ButtonPressCallbackData data)
        {
            if (!data.ButtonIdentifier.Equals("Khanx.AntiFlood.Button"))
                return;

            if (!WaterAntiFlood.coloniesWithWaterEnabled.ContainsKey(data.Player.ActiveColony.ColonyID))
            {
                WaterAntiFlood.coloniesWithWaterEnabled.Add(data.Player.ActiveColony.ColonyID, data.Player.ID);

                Chatting.Chat.Send(data.Player.ActiveColony.Owners, "<color=green>The spread of water in the colony has been enabled.</color>");

                //Force OnUpdateAdjacent to each block of water
                if (ServerManager.ServerSettings.Water.MaxUpdatesPerTick > 0)
                {
                    List<Vector3Int> positions = new List<Vector3Int>();

                    for (int i = 0; i < data.Player.ActiveColony.Banners.Count; i++)
                    {
                        BlockEntities.Implementations.BannerTracker.Banner banner = data.Player.ActiveColony.Banners[i];

                        ForeachBlockInArea(banner.Position - (banner.SafeRadius + 1), banner.Position + (banner.SafeRadius + 1), BlockTypes.BuiltinBlocks.Indices.water, pos =>
                          {
                              WaterAntiFlood.locationsToCheck.AddIfUnique(pos);
                          });
                    }
                }
            }
            else
            {
                WaterAntiFlood.coloniesWithWaterEnabled.Remove(data.Player.ActiveColony.ColonyID);
                Chatting.Chat.Send(data.Player.ActiveColony.Owners, "<color=green>The spread of water in the colony has been disabled.</color>");
            }

            NetworkMenuManager.SendInventoryManageColonyUI(data.Player);
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerDisconnected, "Khanx.AntiFlood.OnPlayerDisconnected")]
        public static void OnPlayerDisconnected(Players.Player player)
        {
            if (player == null)
                return;

            for (int i = 0; i < player.Colonies.Count; i++)
            {
                Colony colony = player.Colonies[i];

                if (WaterAntiFlood.coloniesWithWaterEnabled.ContainsKey(colony.ColonyID))
                    if (WaterAntiFlood.coloniesWithWaterEnabled[colony.ColonyID].Equals(player.ID))
                    {
                        WaterAntiFlood.coloniesWithWaterEnabled.Remove(colony.ColonyID);
                        Chatting.Chat.Send(colony.Owners, "<color=green>The spread of water in the colony has been disabled.</color>");
                    }
            }
        }

        //Code FROM ZUN
        static void ForeachBlockInArea(Vector3Int min, Vector3Int max, ushort type, System.Action<Vector3Int> action)
        {
            Vector3Int chunkMin = min.ToChunk();
            Vector3Int chunkMax = max.ToChunk();
            FindTypeContext context = new FindTypeContext
            {
                Filter = type,
                Action = action,
                Min = min,
                Max = max
            };

            // loop the chunks in the area
            for (int x = chunkMin.x; x <= chunkMax.x; x += 16)
            {
                for (int y = chunkMin.y; y <= chunkMax.y; y += 16)
                {
                    for (int z = chunkMin.z; z <= chunkMax.z; z += 16)
                    {
                        if (World.TryGetChunk(new Vector3Int(x, y, z), out Chunk chunk))
                        {
                            chunk.ForeachData(FindTypeAndRunAction, ref context);
                        }
                    }
                }
            }
        }

        struct FindTypeContext
        {
            public System.Action<Vector3Int> Action;
            public ushort Filter;
            public Vector3Int Min;
            public Vector3Int Max;
        }

        static void FindTypeAndRunAction(ref Chunk.DataIteration iteration, ref FindTypeContext context)
        {
            // if it matches the filter, translate the run into positions and add those to the list
            if (iteration.DataType == context.Filter)
            {
                for (int i = 0; i < iteration.DataCount; i++)
                {
                    Vector3Int pos = iteration.OffsetToWorldPosition(iteration.DataOffset + i);
                    if (pos >= context.Min && pos < context.Max)
                    {
                        context.Action(pos);
                    }
                }
            }
        }

    }
}
