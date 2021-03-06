﻿namespace AtomicTorch.CBND.CoreMod.Items.Weapons.Melee
{
    using System.Collections.Generic;
    using AtomicTorch.CBND.CoreMod.Items.Ammo;
    using AtomicTorch.CBND.CoreMod.Skills;
    using AtomicTorch.CBND.GameApi;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Items;
    using AtomicTorch.CBND.GameApi.Data.Weapons;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    public class ItemNoWeapon : ProtoItemWeaponMelee
    {
        public static ItemNoWeapon Instance { get; private set; }

        [NotLocalizable]
        public override string Description => "Fallback weapon prototype in case no weapon selected.";

        public override ushort DurabilityMax => 0;

        public override ITextureResource Icon => null;

        public override string Name => "No weapon";

        protected override ProtoSkillWeapons WeaponSkill => GetSkill<SkillWeaponsMelee>();

        protected override TextureResource WeaponTextureResource => null;

        public override void ClientSetupSkeleton(
            IItem item,
            ICharacter character,
            IComponentSkeleton skeletonRenderer,
            List<IClientComponent> skeletonComponents)
        {
            // do nothing
        }

        protected override void PrepareProtoItem()
        {
            base.PrepareProtoItem();
            Instance = this;
        }

        protected override void PrepareProtoWeapon(
            out IEnumerable<IProtoItemAmmo> compatibleAmmoProtos,
            ref DamageDescription overrideDamageDescription)
        {
            // no ammo used
            compatibleAmmoProtos = null;

            overrideDamageDescription = new DamageDescription(
                damageValue: 10,
                armorPiercingCoef: 0,
                finalDamageMultiplier: 1,
                rangeMax: 1.2,
                damageDistribution: new DamageDistribution(DamageType.Impact, proportion: 1));
        }
    }
}