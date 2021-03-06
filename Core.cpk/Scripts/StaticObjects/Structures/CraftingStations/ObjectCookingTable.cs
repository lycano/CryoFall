﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Structures.CraftingStations
{
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.Systems.Construction;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    public class ObjectCookingTable : ProtoObjectCraftStation
    {
        public override string Description =>
            "Allows preparation of simple meals and dishes that don't require boiling or frying.";

        public override string Name => "Cooking table";

        public override ObjectSoundMaterial ObjectSoundMaterial => ObjectSoundMaterial.Wood;

        public override double ObstacleBlockDamageCoef => 1.0;

        public override float StructurePointsMax => 300;

        protected override void ClientSetupRenderer(IComponentSpriteRenderer renderer)
        {
            base.ClientSetupRenderer(renderer);
            renderer.DrawOrderOffsetY = 0.5;
        }

        protected override void CreateLayout(StaticObjectLayout layout)
        {
            layout.Setup("##");
        }

        protected override void PrepareConstructionConfig(
            ConstructionTileRequirements tileRequirements,
            ConstructionStageConfig build,
            ConstructionStageConfig repair,
            ConstructionUpgradeConfig upgrade,
            out ProtoStructureCategory category)
        {
            category = GetCategory<StructureCategoryFood>();

            build.StagesCount = 10;
            build.StageDurationSeconds = BuildDuration.Short;
            build.AddStageRequiredItem<ItemPlanks>(count: 4);

            repair.StagesCount = 10;
            repair.StageDurationSeconds = BuildDuration.Short;
            repair.AddStageRequiredItem<ItemPlanks>(count: 1);
        }

        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            var hitboxMelee = CollisionGroups.HitboxMelee;
            var hitboxRanged = CollisionGroups.HitboxRanged;
            var clickArea = CollisionGroups.ClickArea;

            data.PhysicsBody
                // static collider
                .AddShapeRectangle((2, 0.7), offset: (0, 0.1))

                // melee hitbox
                .AddShapeRectangle((1.8, 0.8), offset: (0.1, 0.2), group: hitboxMelee)

                // ranged hitbox
                .AddShapeRectangle((1.8, 0.8), offset: (0.1, 0.2), group: hitboxRanged)

                // click area
                .AddShapeRectangle((1.8, 0.9), offset: (0.1, 0.2), group: clickArea);
        }
    }
}