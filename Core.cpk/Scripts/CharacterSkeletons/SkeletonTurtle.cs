﻿namespace AtomicTorch.CBND.CoreMod.CharacterSkeletons
{
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.GameApi.Data.Physics;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    public class SkeletonTurtle : ProtoCharacterSkeletonAnimal
    {
        public override double DefaultMoveSpeed => 0.2;

        public override SkeletonResource SkeletonResourceBack { get; }
            = new SkeletonResource("Turtle/TurtleBack");

        public override SkeletonResource SkeletonResourceFront { get; }
            = new SkeletonResource("Turtle/TurtleFront");

        public override double WorldScale => 0.4;

        protected override string SoundsFolderPath => "Skeletons/Turtle";

        public override void ClientSetupShadowRenderer(IComponentSpriteRenderer shadowRenderer, double scaleMultiplier)
        {
            shadowRenderer.PositionOffset = (0, 0);
            shadowRenderer.Scale = 1.02 * scaleMultiplier;
        }

        public override void CreatePhysics(IPhysicsBody physicsBody)
        {
            physicsBody.AddShapeRectangle(
                size: (0.6, 0.25),
                offset: (-0.3, -0.05),
                group: CollisionGroups.Default);

            physicsBody.AddShapeCircle(
                radius: 0.35,
                center: (0, 0.25),
                group: CollisionGroups.HitboxMelee);

            physicsBody.AddShapeCircle(
                radius: 0.35,
                center: (0, 0.25),
                group: CollisionGroups.HitboxRanged);
        }
    }
}