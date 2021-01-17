using System.Collections.Generic;


namespace AntiFlood
{
    public class RemoveWater : ModLoaderInterfaces.IOnRegisteringEntityManagers
    {
        public void OnRegisteringEntityManagers(List<object> managers)
        {
            for (int i = 0; i < managers.Count; i++)
            {
                if (managers[i] is BlockEntities.Implementations.Water)
                {
                    managers.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
