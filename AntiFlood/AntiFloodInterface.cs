using ModLoaderInterfaces;
using NetworkUI;
using NetworkUI.Items;

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
                Chatting.Chat.Send(data.Player.ActiveColony.Owners, "<color=green>You have to <b>place and remove a block next to the water</b> so that it can spread.</color>");
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

            foreach (var colony in player.Colonies)
            {
                if (WaterAntiFlood.coloniesWithWaterEnabled.ContainsKey(colony.ColonyID))
                    if (WaterAntiFlood.coloniesWithWaterEnabled[colony.ColonyID].Equals(player.ID))
                    {
                        WaterAntiFlood.coloniesWithWaterEnabled.Remove(colony.ColonyID);
                        Chatting.Chat.Send(colony.Owners, "<color=green>The spread of water in the colony has been disabled.</color>");
                    }
            }
        }
    }
}
