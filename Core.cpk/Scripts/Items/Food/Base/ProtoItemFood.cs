﻿namespace AtomicTorch.CBND.CoreMod.Items.Food
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Windows.Controls;
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.CharacterStatusEffects;
    using AtomicTorch.CBND.CoreMod.CharacterStatusEffects.Debuffs;
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.Stats;
    using AtomicTorch.CBND.CoreMod.Systems.FoodSpoilageSystem;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Items.Controls.SlotOverlays;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Items.Controls.Tooltips;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Items;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.GameEngine.Common.Helpers;

    /// <summary>
    /// Item prototype for food items.
    /// </summary>
    public abstract class ProtoItemFood
        <TPrivateState,
         TPublicState,
         TClientState>
        : ProtoItemGeneric
          <TPrivateState,
              TPublicState,
              TClientState>,
          IProtoItemFood
        where TPrivateState : BasePrivateState, IFoodPrivateState, new()
        where TPublicState : BasePublicState, new()
        where TClientState : BaseClientState, new()
    {
        protected ProtoItemFood()
        {
            this.Icon = new TextureResource("Items/Food/" + this.GetType().Name);
        }

        public virtual float FoodRestore => 0;

        public abstract TimeSpan FreshnessDuration { get; }

        public uint FreshnessMaxValue { get; private set; }

        public virtual float HealthRestore => 0;

        public override ITextureResource Icon { get; }

        public virtual string ItemUseCaption => ItemUseCaptions.Eat;

        /// <summary>
        /// For all food items the default value is "Tiny" stack size.
        /// </summary>
        public override ushort MaxItemsPerStack => ItemStackSize.Small;

        public abstract ushort OrganicValue { get; }

        public sealed override double ServerUpdateIntervalSeconds => 60;

        public virtual float StaminaRestore => 0;

        public virtual float WaterRestore => 0;

        public void ClientCreateItemSlotOverlayControls(IItem item, List<Control> controls)
        {
            if (this.FreshnessMaxValue > 0)
            {
                controls.Add(ItemSlotFreshnessOverlayControl.Create(item));
            }
        }

        public override void ClientTooltipCreateControls(IItem item, List<Control> controls)
        {
            if (this.FreshnessMaxValue > 0)
            {
                controls.Add(ItemTooltipInfoFreshnessControl.Create(item));
            }

            base.ClientTooltipCreateControls(item, controls);
        }

        protected override bool ClientItemUseFinish(ClientItemData data)
        {
            var character = Client.Characters.CurrentPlayerCharacter;
            var item = data.Item;
            var stats = PlayerCharacter.GetPublicState(character).CurrentStatsExtended;
            if (!this.SharedCanEat(
                    new ItemEatData(item,
                                    character,
                                    stats,
                                    FoodSpoilageSystem.SharedGetFreshnessEnum(item))))
            {
                return false;
            }

            this.CallServer(_ => _.ServerRemote_Eat(item));
            return true;
        }

        protected sealed override void PrepareProtoItem()
        {
            base.PrepareProtoItem();
            this.FreshnessMaxValue = FoodSpoilageSystem.SharedCalculateFreshnessMaxValue(this);
            this.PrepareProtoItemFood();
        }

        protected virtual void PrepareProtoItemFood()
        {
        }

        protected override ReadOnlySoundPreset<ItemSound> PrepareSoundPresetItem()
        {
            return ItemsSoundPresets.ItemFood;
        }

        protected override void ServerInitialize(ServerInitializeData data)
        {
            base.ServerInitialize(data);
            FoodSpoilageSystem.ServerInitializeItem(data.PrivateState, data.IsFirstTimeInit);
        }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        protected virtual void ServerOnEat(ItemEatData data)
        {
            var freshnessCoef = FoodSpoilageSystem.SharedGetFreshnessPositiveEffectsCoef(data.Freshness);

            data.CurrentStats.SharedSetStaminaCurrent(data.CurrentStats.StaminaCurrent
                                                      + ApplyFreshness(this.StaminaRestore));

            data.CurrentStats.ServerSetHealthCurrent(data.CurrentStats.HealthCurrent
                                                     + ApplyFreshness(this.HealthRestore));

            data.CurrentStats.ServerSetFoodCurrent(data.CurrentStats.FoodCurrent
                                                   + ApplyFreshness(this.FoodRestore));

            data.CurrentStats.ServerSetWaterCurrent(data.CurrentStats.WaterCurrent
                                                    + ApplyFreshness(this.WaterRestore));

            // Please note: if player has an artificial stomach than the food freshness cannot be red.
            if (data.Freshness == FoodFreshness.Red)
            {
                // 20% chance to get food poisoning
                if (RandomHelper.RollWithProbability(0.2))
                {
                    data.Character.ServerAddStatusEffect<StatusEffectNausea>(intensity: 0.5); // 5 minutes
                }
            }

            float ApplyFreshness(float value)
            {
                if (value <= 0)
                {
                    return value;
                }

                value *= freshnessCoef;
                return value;
            }
        }

        protected override void ServerOnStackItems(ServerOnStackItemData data)
        {
            base.ServerOnStackItems(data);

            // source item
            var item1 = data.ItemFrom;
            var item1State = GetPrivateState(item1);
            var item1Freshness = item1State.FreshnessCurrent;

            // destination item
            var item2 = data.ItemTo;
            var item2State = GetPrivateState(item2);
            var item2Freshness = item2State.FreshnessCurrent;

            var deltaCount = data.CountStacked;

            // calculate average freshness between the already existing (item2) food count
            // and the added food count (item1) and its freshness
            var previousItem2Count = item2.Count - deltaCount;
            var newItem2Freshness = ((ulong)item1Freshness * (ulong)deltaCount
                                     + (ulong)item2Freshness * (ulong)previousItem2Count)
                                    / item2.Count;

            item2State.FreshnessCurrent = (uint)MathHelper.Clamp(newItem2Freshness, 0, uint.MaxValue);
        }

        protected override void ServerUpdate(ServerUpdateData data)
        {
            FoodSpoilageSystem.ServerUpdateFoodFreshness(data.GameObject, data.DeltaTime);
        }

        protected virtual bool SharedCanEat(ItemEatData data)
        {
            return !StatusEffectNausea.SharedCheckIsNauseous(
                       data.Character,
                       showNotificationIfNauseous: true);
        }

        private void ServerRemote_Eat(IItem item)
        {
            var character = ServerRemoteContext.Character;
            this.ServerValidateItemForRemoteCall(item, character);

            var stats = PlayerCharacter.GetPublicState(character).CurrentStatsExtended;

            var freshness = FoodSpoilageSystem.SharedGetFreshnessEnum(item);

            // check that the player has perk to eat a spoiled food
            if (freshness == FoodFreshness.Red
                && character.SharedHasPerk(StatName.PerkEatSpoiledFood))
            {
                freshness = FoodFreshness.Yellow;
            }

            var itemEatData = new ItemEatData(item, character, stats, freshness);
            if (!this.SharedCanEat(itemEatData))
            {
                return;
            }

            this.ServerOnEat(itemEatData);
            Logger.Important(character + " has eaten " + item);

            this.ServerNotifyItemUsed(character, item);
            // decrease item count
            Server.Items.SetCount(item, (ushort)(item.Count - 1));
        }

        [Serializable]
        public struct ItemEatData
        {
            public ItemEatData(
                IItem item,
                ICharacter character,
                PlayerCharacterCurrentStats currentStats,
                FoodFreshness freshness)
            {
                this.Item = item;
                this.Character = character;
                this.CurrentStats = currentStats;
                this.Freshness = freshness;
            }

            public ICharacter Character { get; }

            public PlayerCharacterCurrentStats CurrentStats { get; }

            public FoodFreshness Freshness { get; }

            public IItem Item { get; }
        }
    }

    /// <summary>
    /// Item prototype for food items (without state).
    /// </summary>
    public abstract class ProtoItemFood
        : ProtoItemFood
            <ItemFoodPrivateState,
                EmptyPublicState,
                EmptyClientState>
    {
    }
}