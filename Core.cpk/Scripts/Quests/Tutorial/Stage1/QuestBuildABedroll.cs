﻿namespace AtomicTorch.CBND.CoreMod.Quests.Tutorial
{
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Beds;

    public class QuestBuildABedroll : ProtoQuest
    {
        public override string Description =>
            "You can designate your respawn point by placing a bedroll. Next time your character respawns, they will appear there.";

        public override string Hints =>
            @"[*] Bedroll is located under the ""Other"" category in the construction menu.
              [*] The best strategy is to place the bedroll in your house or base so you can respawn ""at home"".
              [*] Try to find a quiet spot for your first bedroll, far away from other players, a place you can later use for your base. But if you need to move it later—don't worry.";

        public override string Name => "Build a bedroll";

        public override ushort RewardLearningPoints => QuestConstants.TutorialRewardStage1;

        protected override void PrepareQuest(QuestsList prerequisites, RequirementsList requirements)
        {
            requirements
                .Add(RequirementBuildStructure.Require<ObjectBedroll>());

            prerequisites
                .Add<QuestLearnBasicBuilding>();
        }
    }
}