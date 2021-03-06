﻿namespace AtomicTorch.CBND.CoreMod.Technologies.Tier2.Construction2
{
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Decorations;

    public class TechNodeDecorationSkull : TechNode<TechGroupConstruction2>
    {
        protected override void PrepareTechNode(Config config)
        {
            config.Effects
                  .AddStructure<ObjectDecorationSkull>();

            config.SetRequiredNode<TechNodeIronDoor>();
        }
    }
}