﻿namespace AtomicTorch.CBND.CoreMod.Technologies.Tier3.Commerce
{
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.TradingStations;

    public class TechNodeTradingStationLarge : TechNode<TechGroupCommerce>
    {
        protected override void PrepareTechNode(Config config)
        {
            config.Effects
                  .AddStructure<ObjectTradingStationLarge>();

            config.SetRequiredNode<TechNodeTradingStationSmall>();
        }
    }
}