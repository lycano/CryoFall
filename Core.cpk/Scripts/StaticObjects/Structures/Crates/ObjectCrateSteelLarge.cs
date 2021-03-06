﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Crates
{
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.Systems.Construction;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    public class ObjectCrateSteelLarge : ProtoObjectCrate
    {
        public override string Description =>
            "Extra large crate for convenient and safe storage of huge quantities of items.";

        public override byte ItemsSlotsCount => 64;

        public override string Name => "Large crate";

        public override ObjectSoundMaterial ObjectSoundMaterial => ObjectSoundMaterial.Metal;

        public override double ObstacleBlockDamageCoef => 0.5;

        public override double StructureExplosiveDefenseCoef => 0.25;

        public override float StructurePointsMax => 18000;

        protected override void ClientSetupRenderer(IComponentSpriteRenderer renderer)
        {
            base.ClientSetupRenderer(renderer);
            renderer.PositionOffset += (0, 0.4);
            renderer.DrawOrderOffsetY = 0.2;
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
            category = GetCategory<StructureCategoryStorage>();

            build.StagesCount = 10;
            build.StageDurationSeconds = BuildDuration.Short;
            build.AddStageRequiredItem<ItemIngotSteel>(count: 3);

            repair.StagesCount = 10;
            repair.StageDurationSeconds = BuildDuration.Short;
            repair.AddStageRequiredItem<ItemIngotSteel>(count: 1);
        }

        protected override void PrepareDefense(DefenseDescription defense)
        {
            defense.Set(ObjectDefensePresets.Tier3);
        }

        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            data.PhysicsBody
                .AddShapeRectangle(size: (2, 0.5), offset: (0, 0.4))
                .AddShapeRectangle(
                    size: (2, 0.75),
                    offset: (0, 0.4),
                    group: CollisionGroups.HitboxMelee)
                .AddShapeRectangle(
                    size: (2, 0.75),
                    offset: (0, 0.4),
                    group: CollisionGroups.HitboxRanged)
                .AddShapeRectangle(
                    size: (2, 0.75),
                    offset: (0, 0.4),
                    group: CollisionGroups.ClickArea);
        }
    }
}