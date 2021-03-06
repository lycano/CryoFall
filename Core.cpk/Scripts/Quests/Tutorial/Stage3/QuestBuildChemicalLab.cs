﻿namespace AtomicTorch.CBND.CoreMod.Quests.Tutorial
{
    using AtomicTorch.CBND.CoreMod.CraftRecipes;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.CraftingStations;

    public class QuestBuildChemicalLab : ProtoQuest
    {
        public override string Description =>
            "Advances in industry and construction require basis in hard science, especially chemistry.";

        public override string Hints =>
            "[*] Chemistry is one of the most important areas of science, and it is recommended for most survivors.";

        public override string Name => "Build chemical lab";

        public override ushort RewardLearningPoints => QuestConstants.TutorialRewardStage3;

        protected override void PrepareQuest(QuestsList prerequisites, RequirementsList requirements)
        {
            requirements
                .Add(RequirementBuildStructure.Require<ObjectChemicalLab>())
                .Add(RequirementCraftRecipe.RequireStationRecipe<RecipeAcidSulfuricFromPyrite>())
                .Add(RequirementCraftRecipe.RequireStationRecipe<RecipeAcidNitric>())
                .Add(RequirementCraftRecipe.RequireStationRecipe<RecipeNitrocellulosePowder>());

            prerequisites
                .Add<QuestCompleteTier1Technologies>();
        }
    }
}