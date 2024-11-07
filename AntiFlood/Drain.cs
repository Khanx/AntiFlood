using BlockTypes;
using Pipliz;
using System.Collections.Generic;

namespace AntiFlood
{
    public class Drain
    {
        private static readonly Vector3Int[] adjacents = { Vector3Int.left, Vector3Int.forward, Vector3Int.right, Vector3Int.back, Vector3Int.up, Vector3Int.down };

        public static void DrainAdjacentWaterInColony(Players.Player player)
        {
            Vector3Int position = new Vector3Int(player.Position);

            if (World.TryGetTypeAt(position, out ushort type) && type != BuiltinBlocks.Indices.water)
            {
                Chatting.Chat.Send(player, "<color=red>You must be inside the water to be able to drain it.</color>");
                return;
            }

            bool foundBanner = ServerManager.BlockEntityTracker.BannerTracker.TryGetClosest(position, out var banner);

            if (!foundBanner || ((position - banner.Position).MaxPartAbs > banner.SafeRadius))
            {
                Chatting.Chat.Send(player, "<color=red>You can only drain water within the area of ​​your colony.</color>");
                return;
            }

            /*
            //Disables the water spread inside of the banner area
            if(WaterAntiFlood.coloniesWithWaterEnabled.ContainsKey(player.ActiveColony.ColonyGroup.MainColonyID))
            {
                WaterAntiFlood.coloniesWithWaterEnabled.Remove(player.ActiveColony.ColonyGroup.MainColonyID);
                Chatting.Chat.Send(player.ActiveColonyGroup.Owners, "<color=green>The spread of water in the colony has been disabled.</color>");
            }
            */

            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(position);

            Chatting.Chat.Send(player.ActiveColonyGroup.Owners, "<color=green>Draining water, it may take some time.</color>");
            RemoveWater(queue, banner.Position, banner.SafeRadius);
        }

        private static readonly int MaxChangesPerTick = 2048;
        private static readonly double TimeBetweenTicks = 0.2;

        /// <summary>
        /// This is a RECURSIVE method that removes MaxChangesPerTick every TimeBetweenTicks
        /// </summary>
        /// <param name="position">Position from which to start draining</param>
        /// <param name="bannerPosition">Position of the closest banner.</param>
        /// <param name="safeRadius">Area around the banner from which water drains.</param>
        private static void RemoveWater(Queue<Vector3Int> positions, Vector3Int bannerPosition, int safeRadius)
        {
            int changes = 0;

            while (positions.Count > 0)
            {
                Vector3Int position = positions.Dequeue();
                changes++;

                ServerManager.TryChangeBlock(position, BuiltinBlocks.Indices.water, BuiltinBlocks.Indices.air, flags: ESetBlockFlags.Default & ~ESetBlockFlags.TriggerNeighbourCallbacks);

                for (int i = 0; i < adjacents.Length; i++)
                {
                    Vector3Int positionToCheck = position + adjacents[i];

                    if ((positionToCheck - bannerPosition).MaxPartAbs > safeRadius)
                        continue;

                    World.TryGetTypeAt(positionToCheck, out ushort type);

                    if (type == BuiltinBlocks.Indices.water && !positions.Contains(positionToCheck))
                        positions.Enqueue(positionToCheck);
                }

                if (changes >= MaxChangesPerTick)
                {
                    ThreadManager.InvokeOnMainThread(delegate
                    {
                        RemoveWater(positions, bannerPosition, safeRadius);

                    }, TimeBetweenTicks);
                    break;
                }
            }
        }
    }
}
