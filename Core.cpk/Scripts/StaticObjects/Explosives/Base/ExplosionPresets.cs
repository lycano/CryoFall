﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Explosives
{
    using System.Windows.Media;
    using AtomicTorch.GameEngine.Common.Primitives;

    public static class ExplosionPresets
    {
        public static readonly ExplosionPreset ExtraLargePragmiumResonanceBomb
            = ExplosionPreset.CreatePreset(
                serverDamageApplyDelay: 0.8 * 0.25,
                soundSetPath: "Explosions/ExplosionLarge",
                spriteAnimationDuration: 0.9,
                spriteColorAdditive: Color.FromRgb(0x00,       0x44, 0x88),
                spriteColorMultiplicative: Color.FromRgb(0xAA, 0xFF, 0xFF),
                spriteBrightness: 1.33,
                // use only this particular sprite
                spriteSetPath: "FX/Explosions/ExplosionLarge3",
                spriteAtlasColumns: 8,
                spriteAtlasRows: 3,
                spriteWorldSize: new Size2F(3, 3),
                blastwaveDelay: 0.1,
                blastwaveAnimationDuration: 0.75,
                blastWaveColor: Color.FromRgb(0xBB, 0xDD, 0xFF),
                blastwaveWorldSizeFrom: 0.25 * new Size2F(6, 4),
                blastwaveWorldSizeTo: 1 * new Size2F(6,      4),
                lightDuration: 1,
                lightWorldSize: 37.5,
                lightColor: Color.FromRgb(0x88, 0xDD, 0xFF),
                screenShakesDuration: 0.3,
                screenShakesWorldDistanceMin: 0.2,
                screenShakesWorldDistanceMax: 0.25);

        public static readonly ExplosionPreset Large
            = ExplosionPreset.CreatePreset(
                serverDamageApplyDelay: 0.8 * 0.25,
                soundSetPath: "Explosions/ExplosionLarge",
                spriteAnimationDuration: 0.8,
                spriteSetPath: "FX/Explosions/ExplosionLarge",
                spriteAtlasColumns: 8,
                spriteAtlasRows: 3,
                spriteWorldSize: new Size2F(2, 2),
                blastwaveDelay: 0.1,
                blastwaveAnimationDuration: 0.6,
                blastWaveColor: Color.FromRgb(0xFF, 0xBB, 0x33),
                blastwaveWorldSizeFrom: 0.25 * new Size2F(3, 2),
                blastwaveWorldSizeTo: 1 * new Size2F(3,      2),
                lightDuration: 1,
                lightWorldSize: 35,
                lightColor: Color.FromRgb(0xFF, 0xCC, 0x66),
                screenShakesDuration: 0.2,
                screenShakesWorldDistanceMin: 0.2,
                screenShakesWorldDistanceMax: 0.25);

        public static readonly ExplosionPreset SpecialDepositExplosion
            = ExplosionPreset.CreatePreset(
                serverDamageApplyDelay: 0.8 * 0.25,
                soundSetPath: "Explosions/ExplosionLarge",
                spriteAnimationDuration: 1,
                spriteBrightness: 1.33,
                // use only this particular sprite
                spriteSetPath: "FX/Explosions/ExplosionLarge3",
                spriteAtlasColumns: 8,
                spriteAtlasRows: 3,
                spriteWorldSize: new Size2F(3, 3),
                blastwaveDelay: 0.1,
                blastwaveAnimationDuration: 0.6,
                blastWaveColor: Color.FromRgb(0xFF, 0xBB, 0x33),
                blastwaveWorldSizeFrom: 0.25 * new Size2F(9, 6),
                blastwaveWorldSizeTo: 1 * new Size2F(9,      6),
                lightDuration: 1,
                lightWorldSize: 40,
                lightColor: Color.FromRgb(0xFF, 0xCC, 0x66),
                screenShakesDuration: 0.4,
                screenShakesWorldDistanceMin: 0.3,
                screenShakesWorldDistanceMax: 0.35);

        public static readonly ExplosionPreset SpecialPragmiumSourceExplosion
            = ExplosionPreset.CreatePreset(
                serverDamageApplyDelay: 0.8 * 0.25,
                soundSetPath: "Explosions/ExplosionLarge",
                spriteAnimationDuration: 1,
                spriteColorAdditive: Color.FromRgb(0x00,       0x44, 0x88),
                spriteColorMultiplicative: Color.FromRgb(0xAA, 0xFF, 0xFF),
                spriteBrightness: 1.33,
                // use only this particular sprite
                spriteSetPath: "FX/Explosions/ExplosionLarge3",
                spriteAtlasColumns: 8,
                spriteAtlasRows: 3,
                spriteWorldSize: new Size2F(4, 4),
                blastwaveDelay: 0.1,
                blastwaveAnimationDuration: 0.6,
                blastWaveColor: Color.FromRgb(0xBB, 0xDD, 0xFF),
                blastwaveWorldSizeFrom: 0.25 * new Size2F(9, 6),
                blastwaveWorldSizeTo: 1 * new Size2F(9,      6),
                lightDuration: 1,
                lightWorldSize: 40,
                lightColor: Color.FromRgb(0x88, 0xDD, 0xFF),
                screenShakesDuration: 0.4,
                screenShakesWorldDistanceMin: 0.3,
                screenShakesWorldDistanceMax: 0.35);
    }
}