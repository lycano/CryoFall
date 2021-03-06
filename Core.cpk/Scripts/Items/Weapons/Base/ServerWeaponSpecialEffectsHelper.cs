﻿namespace AtomicTorch.CBND.CoreMod.Items.Weapons
{
    using AtomicTorch.CBND.CoreMod.CharacterStatusEffects;
    using AtomicTorch.CBND.CoreMod.CharacterStatusEffects.Debuffs;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.GameEngine.Common.Helpers;

    public static class ServerWeaponSpecialEffectsHelper
    {
        public static void OnAxeHit(ICharacter damagedCharacter, double damage)
        {
            if (damage < 5)
            {
                return;
            }

            damagedCharacter.ServerAddStatusEffect<StatusEffectBleeding>(intensity: 0.1); // 1 minute
            damagedCharacter.ServerAddStatusEffect<StatusEffectPain>(intensity: 0.2); // 20 seconds

            // 2% to add broken leg status effect
            if (RandomHelper.RollWithProbability(0.02))
            {
                damagedCharacter.ServerAddStatusEffect<StatusEffectBrokenLeg>(intensity: 1);
            }

            // 2% to add dazed status effect
            if (RandomHelper.RollWithProbability(0.02))
            {
                damagedCharacter.ServerAddStatusEffect<StatusEffectDazed>(
                    intensity: 1.0 / StatusEffectDazed.MaxDuration); // 1 second
            }
        }

        public static void OnFirearmHit(ICharacter damagedCharacter, double damage)
        {
            if (damage < 5)
            {
                return;
            }

            damagedCharacter.ServerAddStatusEffect<StatusEffectBleeding>(intensity: 0.1); // 1 minute
        }

        public static void OnKnifeHit(ICharacter damagedCharacter, double damage)
        {
            if (damage < 5)
            {
                return;
            }

            damagedCharacter.ServerAddStatusEffect<StatusEffectBleeding>(intensity: 0.05); // 0.5 minute
        }

        public static void OnLaserHit(ICharacter damagedCharacter, double damage)
        {
            if (damage < 5)
            {
                return;
            }

            // TODO: currently there are no reasonable effects to add to energy weapons, consider adding something later, maybe more damage to armor or something else
            damagedCharacter.ServerAddStatusEffect<StatusEffectPain>(intensity: 0.1);
        }

        public static void OnLaserRapierHit(ICharacter damagedCharacter, double damage)
        {
            if (damage < 5)
            {
                return;
            }

            damagedCharacter.ServerAddStatusEffect<StatusEffectPain>(intensity: 0.1);

            // TODO: yeah... this one doesn't make much sense, so it is probably a good idea to replace with a different effect when we have a few more
            damagedCharacter.ServerAddStatusEffect<StatusEffectRadiationPoisoning>(intensity: 0.1);
        }

        public static void OnMaceHit(ICharacter damagedCharacter, double damage)
        {
            if (damage < 5)
            {
                return;
            }

            damagedCharacter.ServerAddStatusEffect<StatusEffectDazed>(
                intensity: 3.0 / StatusEffectDazed.MaxDuration); // 3 seconds
        }

        public static void OnPickaxeHit(ICharacter damagedCharacter, double damage)
        {
            if (damage < 5)
            {
                return;
            }

            damagedCharacter.ServerAddStatusEffect<StatusEffectBleeding>(intensity: 0.1); // 1 minute
            damagedCharacter.ServerAddStatusEffect<StatusEffectPain>(intensity: 0.2); // 20 seconds

            // 2% to add broken leg status effect
            if (RandomHelper.RollWithProbability(0.02))
            {
                damagedCharacter.ServerAddStatusEffect<StatusEffectBrokenLeg>(intensity: 1);
            }

            // 2% to add dazed status effect
            if (RandomHelper.RollWithProbability(0.02))
            {
                damagedCharacter.ServerAddStatusEffect<StatusEffectDazed>(
                    intensity: 1.0 / StatusEffectDazed.MaxDuration); // 1 second
            }
        }

        public static void OnShotgunHit(ICharacter damagedCharacter, double damage)
        {
            if (damage < 5)
            {
                return;
            }

            damagedCharacter.ServerAddStatusEffect<StatusEffectBleeding>(intensity: 0.2); // 2 minutes

            // 2% to add dazed status effect
            if (RandomHelper.RollWithProbability(0.02))
            {
                damagedCharacter.ServerAddStatusEffect<StatusEffectDazed>(
                    intensity: 1.0 / StatusEffectDazed.MaxDuration); // 1 second
            }
        }
    }
}