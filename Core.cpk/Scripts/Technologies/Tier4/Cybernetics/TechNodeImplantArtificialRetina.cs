﻿namespace AtomicTorch.CBND.CoreMod.Technologies.Tier4.Cybernetics
{
    using AtomicTorch.CBND.CoreMod.CraftRecipes;

    public class TechNodeImplantArtificialRetina : TechNode<TechGroupCybernetics>
    {
        protected override void PrepareTechNode(Config config)
        {
            config.Effects
                  .AddRecipe<RecipeImplantArtificialRetina>();

            config.SetRequiredNode<TechNodeImplantNanoFiberSkin>();
        }
    }
}