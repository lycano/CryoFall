﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Props.Bunker
{
    public class ObjectPropBunkerRoof : ProtoObjectProp
    {
        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            data.PhysicsBody
                .AddShapeRectangle(size: (1, 1), offset: (0, 0));
        }
    }
}