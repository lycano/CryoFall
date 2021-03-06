﻿namespace AtomicTorch.CBND.CoreMod.Items.Equipment
{
    public class ItemFurCoat : ProtoItemEquipmentChest
    {
        public override string Description =>
            "Decent early armor; especially suitable to protect against cold. Not very expensive, and it's comfy!";

        public override ushort DurabilityMax => 800;

        public override string Name => "Fur coat";

        protected override void PrepareDefense(DefenseDescription defense)
        {
            defense.Set(
                impact: 0.45,
                kinetic: 0.40,
                heat: 0.15,
                cold: 0.65,
                chemical: 0.15,
                electrical: 0.20,
                radiation: 0.20,
                psi: 0);
        }
    }
}