﻿namespace AtomicTorch.CBND.CoreMod.CharacterSkeletons
{
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.GameApi.Data.Physics;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    public class SkeletonSnakeBrown : ProtoCharacterSkeletonAnimal
    {
        public override SkeletonResource SkeletonResourceBack { get; }
            = new SkeletonResource("SnakeBrown/SnakeBack");

        public override SkeletonResource SkeletonResourceFront { get; }
            = new SkeletonResource("SnakeBrown/SnakeFront");

        public override double WorldScale => 0.4;

        protected override string SoundsFolderPath => "Skeletons/Snake";

        public override void ClientSetupShadowRenderer(IComponentSpriteRenderer shadowRenderer, double scaleMultiplier)
        {
            shadowRenderer.PositionOffset = (0, -0.01 * scaleMultiplier);
            shadowRenderer.Scale = 0.7 * scaleMultiplier;
        }

        public override void CreatePhysics(IPhysicsBody physicsBody)
        {
            physicsBody.AddShapeRectangle(
                size: (0.6, 0.25),
                offset: (-0.3, -0.05),
                group: CollisionGroups.Default);

            physicsBody.AddShapeCircle(
                radius: 0.42,
                center: (0, 0.35),
                group: CollisionGroups.HitboxMelee);

            physicsBody.AddShapeCircle(
                radius: 0.42,
                center: (0, 0.35),
                group: CollisionGroups.HitboxRanged);
        }
    }
}