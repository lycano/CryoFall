﻿namespace AtomicTorch.CBND.CoreMod.Zones
{
    using System;
    using AtomicTorch.CBND.CoreMod.Characters.Mobs;
    using AtomicTorch.CBND.CoreMod.Triggers;

    public class SpawnMobsScorpion : ProtoZoneSpawnScript
    {
        protected override void PrepareZoneSpawnScript(Triggers triggers, SpawnList spawnList)
        {
            triggers
                .Add(GetTrigger<TriggerWorldInit>())
                .Add(GetTrigger<TriggerTimeInterval>().Configure(TimeSpan.FromMinutes(10)));

            spawnList.CreatePreset(interval: 30, padding: 0.5, useSectorDensity: false)
                     .Add<MobScorpion>()
                     .SetCustomPaddingWithSelf(35);
        }
    }
}