﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Explosives.Bombs
{
    using System;
    using System.Windows.Media;
    using AtomicTorch.CBND.CoreMod.ClientComponents.Rendering;
    using AtomicTorch.CBND.CoreMod.ClientComponents.Rendering.Lighting;
    using AtomicTorch.CBND.GameApi.Data.Weapons;
    using AtomicTorch.CBND.GameApi.Resources;

    public class ObjectBombModern : ProtoObjectExplosive
    {
        private ITextureAtlasResource atlasTexture;

        public override double DamageRadius => 2.1;

        public override string Name => "Modern bomb";

        protected override void ClientInitialize(ClientInitializeData data)
        {
            base.ClientInitialize(data);
            data.ClientState.Renderer.DrawOrderOffsetY = 0.355;

            // add sprite sheet animation
            var sceneObject = Client.Scene.GetSceneObject(data.GameObject);
            sceneObject
                .AddComponent<ClientComponentSpriteSheetAnimator>()
                .Setup(data.ClientState.Renderer,
                       ClientComponentSpriteSheetAnimator.CreateAnimationFrames(this.atlasTexture),
                       frameDurationSeconds: 0.15f);

            // add light source at the firing fuse
            ClientLighting.CreateLightSourceSpot(
                sceneObject,
                Color.FromRgb(0xFF, 0x55, 0x22),
                size: (3, 3),
                positionOffset: (0.4, 0.4));
        }

        protected override void PrepareDamageDescriptionCharacters(
            out double damageValue,
            out double armorPiercingCoef,
            out double finalDamageMultiplier,
            DamageDistribution damageDistribution)
        {
            damageValue = 120;
            armorPiercingCoef = 0;
            finalDamageMultiplier = 1;
            damageDistribution.Set(DamageType.Kinetic, 1);
        }

        protected override void PrepareDamageDescriptionStructures(
            out double damageValue,
            out double defencePenetrationCoef)
        {
            damageValue = 6000;
            defencePenetrationCoef = 0.5;
        }

        protected override ITextureResource PrepareDefaultTexture(Type thisType)
        {
            this.atlasTexture = new TextureAtlasResource(GenerateTexturePath(thisType) + "Animation",
                                                         columns: 2,
                                                         rows: 1,
                                                         isTransparent: true);

            return this.atlasTexture.Chunk(0, 0);
        }

        protected override void PrepareProtoObjectExplosive(out ExplosionPreset explosionPresets)
        {
            explosionPresets = ExplosionPresets.Large;
        }
    }
}