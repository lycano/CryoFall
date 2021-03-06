﻿namespace AtomicTorch.CBND.CoreMod.CraftRecipes.DryingCabinet
{
    using System;
    using AtomicTorch.CBND.CoreMod.Items.Food;
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Manufacturers;
    using AtomicTorch.CBND.CoreMod.Systems;
    using AtomicTorch.CBND.CoreMod.Systems.Crafting;

    public class RecipeMeatJerky : Recipe.RecipeForManufacturing
    {
        protected override void SetupRecipe(
            StationsList stations,
            out TimeSpan duration,
            InputItems inputItems,
            OutputItems outputItems)
        {
            stations.Add<ObjectDryingCabinet>();

            duration = CraftingDuration.VeryLong;

            inputItems.Add<ItemMeatRaw>();
            inputItems.Add<ItemSalt>(count: 2);

            outputItems.Add<ItemMeatJerky>();
        }
    }
}