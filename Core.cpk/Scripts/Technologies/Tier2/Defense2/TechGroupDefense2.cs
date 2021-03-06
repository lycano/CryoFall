﻿namespace AtomicTorch.CBND.CoreMod.Technologies.Tier2.Defense2
{
    using AtomicTorch.CBND.CoreMod.Technologies.Tier1.Industry;
    using AtomicTorch.CBND.CoreMod.Technologies.Tier1.OffenseAndDefense;

    public class TechGroupDefense2 : TechGroup
    {
        public override string Description => "Improved protection technologies offer better armor schematics.";

        public override string Name => "Defense 2";

        public override TechTier Tier => TechTier.Tier2;

        protected override void PrepareTechGroup(Requirements requirements)
        {
            requirements.AddGroup<TechGroupOffenseAndDefense>(completion: 0.8);
            requirements.AddGroup<TechGroupIndustry>(completion: 0.6);
        }
    }
}