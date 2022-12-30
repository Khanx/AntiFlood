using BlockEntities;
using System.Collections.Generic;


namespace AntiFlood
{
    public class RemoveWater : ModLoaderInterfaces.IOnRegisteringEntityManagers
    {
        public void OnRegisteringEntityManagers(IEntityManager[] managers)
        {
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] is BlockEntities.Implementations.Water)
                {
                    managers[i] = new WaterAntiFlood();
                    return;
                }
            }
        }
    }
}
