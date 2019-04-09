﻿namespace AtomicTorch.CBND.CoreMod.Items.Seeds
{
    using System.Collections.Generic;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Farms;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Vegetation;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Vegetation.Plants;

    public class ItemSeedsTomato : ProtoItemSeed
    {
        public override string Description =>
            "Tomato seeds for a very juicy variety of tomatoes specifically selected for this planet.";

        public override string Name => "Tomato seeds";

        protected override void PrepareProtoItemSeed(
            out IProtoObjectVegetation objectPlantProto,
            List<IProtoObjectFarm> allowedToPlaceAt)
        {
            objectPlantProto = GetPlant<ObjectPlantTomato>();

            allowedToPlaceAt.Add(GetPlot<ObjectFarmPlot>());
        }
    }
}