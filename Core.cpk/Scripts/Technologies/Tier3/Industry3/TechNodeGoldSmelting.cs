﻿namespace AtomicTorch.CBND.CoreMod.Technologies.Tier3.Industry3
{
    using AtomicTorch.CBND.CoreMod.CraftRecipes;

    public class TechNodeGoldSmelting : TechNode<TechGroupIndustry3>
    {
        public override string Name => "Gold smelting";

        protected override void PrepareTechNode(Config config)
        {
            config.Effects
                  .AddRecipe<RecipeIngotGold>();

            config.SetRequiredNode<TechNodeBombMining>();
        }
    }
}