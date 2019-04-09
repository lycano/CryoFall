﻿namespace AtomicTorch.CBND.CoreMod.Items.Equipment
{
    public class ItemLeatherHatTricorne : ProtoItemEquipmentHead
    {
        public override string Description => GetProtoEntity<ItemLeatherJacket>().Description;

        public override ushort DurabilityMax => 800;

        public override bool IsHairVisible => false;

        public override string Name => "Pirate hat";

        protected override void PrepareDefense(DefenseDescription defense)
        {
            defense.Set(
                impact: 0.50,
                kinetic: 0.40,
                heat: 0.25,
                cold: 0.30,
                chemical: 0.20,
                electrical: 0.25,
                radiation: 0.20,
                psi: 0);
        }
    }
}