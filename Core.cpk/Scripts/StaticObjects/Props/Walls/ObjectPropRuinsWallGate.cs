﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Props.Walls
{
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.GameApi.Data.World;

    public class ObjectPropRuinsWallGate : ProtoObjectProp
    {
        public override bool CanFlipSprite => true;

        public override ObjectSoundMaterial ObjectSoundMaterial
            => ObjectSoundMaterial.Metal;

        protected override void CreateLayout(StaticObjectLayout layout)
        {
            layout.Setup("##");
        }

        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            data.PhysicsBody
                .AddShapeRectangle(size: (2, 0.5), offset: (0, 0));
        }
    }
}