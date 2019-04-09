﻿namespace AtomicTorch.CBND.CoreMod.Skills
{
    using AtomicTorch.CBND.CoreMod.Stats;

    public class SkillAthletics : ProtoSkill
    {
        public const double ExperienceAddWhenRunningPerSecond = 15.0;

        public override string Description =>
            "Training makes your body more accustomed to long-distance running. As a result, you use less energy while sprinting.";

        public override double ExperienceToLearningPointsConversionMultiplier =>
            0.25; // this is 1/4 of the standard conversion, because athletics is very quick to advance

        public override bool IsSharingLearningPointsWithPartyMembers => false;

        public override string Name => "Athletics";

        protected override void PrepareProtoSkill(SkillConfig config)
        {
            config.Category = GetCategory<SkillCategoryPersonal>();

            var statName = StatName.RunningStaminaConsumptionPerSecond;
            config.AddStatEffect(
                statName,
                level: 1,
                percentBonus: -5);

            config.AddStatEffect(
                statName,
                formulaPercentBonus: level => -2 * level);

            config.AddStatEffect(
                statName,
                level: this.MaxLevel,
                percentBonus: -5);
        }
    }
}