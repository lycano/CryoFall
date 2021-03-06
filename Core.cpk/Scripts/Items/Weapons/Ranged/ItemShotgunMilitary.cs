﻿namespace AtomicTorch.CBND.CoreMod.Items.Weapons.Ranged
{
    using System.Collections.Generic;
    using AtomicTorch.CBND.CoreMod.Items.Ammo;
    using AtomicTorch.CBND.CoreMod.Skills;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Weapons;

    public class ItemShotgunMilitary : ProtoItemWeaponRanged
    {
        public override ushort AmmoCapacity => 6;

        public override double AmmoReloadDuration => 2;

        public override double CharacterAnimationAimingRecoilDuration => 0.3;

        public override double CharacterAnimationAimingRecoilPower => 0.7;

        public override string Description => "Military shotgun with large ammo capacity. Uses 12ga ammunition.";

        public override ushort DurabilityMax => 250;

        public override double FireInterval => 0.4;

        public override string Name => "Military shotgun";

        public override double SpecialEffectProbability => 0.3;

        protected override ProtoSkillWeapons WeaponSkill => GetSkill<SkillWeaponsConventional>();

        protected override void PrepareMuzzleFlashDescription(MuzzleFlashDescription description)
        {
            description.Set(MuzzleFlashPresets.ModernShotgun)
                       .Set(textureScreenOffset: (17, 10));
        }

        protected override void PrepareProtoWeaponRanged(
            out IEnumerable<IProtoItemAmmo> compatibleAmmoProtos,
            ref DamageDescription overrideDamageDescription)
        {
            compatibleAmmoProtos = GetAmmoOfType<IAmmoCaliber12g>();
        }

        protected override ReadOnlySoundPreset<WeaponSound> PrepareSoundPresetWeapon()
        {
            return WeaponsSoundPresets.WeaponRangedShotgun;
        }

        protected override void ServerOnSpecialEffect(ICharacter damagedCharacter, double damage)
        {
            ServerWeaponSpecialEffectsHelper.OnShotgunHit(damagedCharacter, damage);
        }
    }
}