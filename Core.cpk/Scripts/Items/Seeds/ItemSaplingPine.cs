﻿namespace AtomicTorch.CBND.CoreMod.Items.Seeds
{
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Vegetation;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Vegetation.Trees;

    public class ItemSaplingPine : ProtoItemSapling, IProtoItemOrganic
    {
        public override string Description => "Sapling from a pine tree. Can be planted to grow a new tree.";

        public override string Name => "Pine tree sapling";

        public ushort OrganicValue => 2;

        protected override void PrepareProtoItemSapling(out IProtoObjectVegetation objectPlantProto)
        {
            objectPlantProto = GetPlant<ObjectTreePine>();
        }
    }
}