﻿namespace AtomicTorch.CBND.CoreMod.CharacterStatusEffects
{
    using System;
    using System.Collections.Generic;
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Stats;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.GameEngine.Common.Helpers;

    /// <summary>
    /// Special status effect which depends on nearby objects to increase/decrease.
    /// </summary>
    public abstract class ProtoRadiantStatusEffect : ProtoStatusEffect
    {
        private static readonly List<IWorldObject> TempResult
            = new List<IWorldObject>(capacity: 512);

        public sealed override double IntensityAutoDecreasePerSecondFraction =>
            0; // doesn't decrease via base implementation

        public sealed override double IntensityAutoDecreasePerSecondValue =>
            0; // doesn't decrease via base implementation

        public override double ServerAutoAddRepeatIntervalSeconds => 0.5;

        public override double ServerUpdateIntervalSeconds => 0.5;

        protected abstract StatName DefenseStatName { get; }

        /// <summary>
        /// Time to remove full effect intensity back to zero in case the environmental intensity is 0.
        /// </summary>
        protected abstract double TimeToCoolDownToZeroSeconds { get; }

        /// <summary>
        /// Time to reach the full intensity in case the environmental intensity is 1.
        /// </summary>
        protected abstract double TimeToReachFullIntensitySeconds { get; }

        protected virtual double ServerCalculateEnvironmentalIntensityAroundCharacter(ICharacter character)
        {
            Server.World.GetWorldObjectsInView(character, TempResult, sortByDistance: false);

            var result = 0.0;
            foreach (var worldObject in TempResult)
            {
                var objectHeatIntensity = this.SharedCalculateObjectEnvironmentalIntensity(character,
                                                                                           worldObject);

                if (result < objectHeatIntensity)
                {
                    result = objectHeatIntensity;
                }
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (result == 0)
            {
                return result;
            }

            // Armor blocks maximum level of effect.
            // E.g. 75% armor means that effect intensity cannot reach higher than 25%.
            var defense = character.SharedGetFinalStatValue(this.DefenseStatName);
            defense = MathHelper.Clamp(defense, 0, 1);
            result = Math.Min(result, 1 - defense);

            return result;
        }

        protected override void ServerOnAutoAdd(ICharacter character)
        {
            var environmentalIntensity = this.ServerCalculateEnvironmentalIntensityAroundCharacter(character);
            var currentIntensity = character.SharedGetStatusEffectIntensity(this);

            var delta = environmentalIntensity - currentIntensity;
            if (delta > 0)
            {
                // need to add the intensity
                var speed = 1.0 / (environmentalIntensity * this.TimeToReachFullIntensitySeconds);
                delta = Math.Min(delta,
                                 speed * this.ServerAutoAddRepeatIntervalSeconds);
            }
            else if (delta < 0)
            {
                // need to reduce the intensity
                var speed = 1.0 / ((environmentalIntensity - 1) * this.TimeToCoolDownToZeroSeconds);
                delta = Math.Max(delta,
                                 speed * this.ServerAutoAddRepeatIntervalSeconds);
            }
            else
            {
                // no need to change the intensity
                return;
            }

            var newIntensity = currentIntensity + delta;
            character.ServerSetStatusEffectIntensity(this, intensity: newIntensity);
        }

        protected abstract double SharedCalculateObjectEnvironmentalIntensity(
            ICharacter character,
            IWorldObject worldObject);
    }
}