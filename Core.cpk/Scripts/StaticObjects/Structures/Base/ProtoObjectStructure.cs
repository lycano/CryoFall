﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Structures
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.ClientComponents.StaticObjects;
    using AtomicTorch.CBND.CoreMod.Items.Tools.Toolboxes;
    using AtomicTorch.CBND.CoreMod.Items.Weapons.Melee;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.Systems.Construction;
    using AtomicTorch.CBND.CoreMod.Systems.Creative;
    using AtomicTorch.CBND.CoreMod.Systems.LandClaim;
    using AtomicTorch.CBND.CoreMod.Systems.Notifications;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.CoreMod.Systems.StructureDecaySystem;
    using AtomicTorch.CBND.CoreMod.Systems.Weapons;
    using AtomicTorch.CBND.CoreMod.Technologies;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Items.Controls;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects.ConstructionTooltip;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Physics;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;
    using AtomicTorch.GameEngine.Common.Extensions;
    using AtomicTorch.GameEngine.Common.Helpers;
    using AtomicTorch.GameEngine.Common.Primitives;

    public abstract class ProtoObjectStructure
        <TPrivateState,
         TPublicState,
         TClientState>
        : ProtoStaticWorldObject
          <TPrivateState,
              TPublicState,
              TClientState>,
          IProtoObjectStructure
        where TPrivateState : StructurePrivateState, new()
        where TPublicState : StaticObjectPublicState, new()
        where TClientState : StaticObjectClientState, new()
    {
        public const string NotificationUseWeaponOrTool = "Use a weapon or a tool to damage this structure.";

        private IComponentAttachedControl currentDisplayedRepairTooltip;

        private List<TechNode> listedInTechNodes;

        [SuppressMessage("ReSharper", "CanExtractXamlLocalizableStringCSharp")]
        protected ProtoObjectStructure()
        {
            var type = this.GetType();
            var typeName = type.Name;
            var name = typeName;
            if (name.StartsWith("Object"))
            {
                name = name.Substring("Object".Length);
            }

            this.ShortId = name;
        }

        public ProtoStructureCategory Category { get; private set; }

        public IConstructionStageConfigReadOnly ConfigBuild { get; private set; }

        public IConstructionStageConfigReadOnly ConfigRepair { get; private set; }

        public IConstructionUpgradeConfigReadOnly ConfigUpgrade { get; private set; }

        /// <summary>
        /// Text description.
        /// </summary>
        public virtual string Description => "No description.";

        /// <summary>
        /// Text description for this structure when it's displayed as an upgrade for another structure.
        /// </summary>
        public virtual string DescriptionUpgrade => "No upgrade description.";

        public virtual bool IsAutoUnlocked => false;

        public override StaticObjectKind Kind => StaticObjectKind.Structure;

        public IReadOnlyList<TechNode> ListedInTechNodes
            => this.listedInTechNodes
               ?? (IReadOnlyList<TechNode>)Array.Empty<TechNode>();

        public override string ShortId { get; }

        public virtual float StructurePointsMaxForConstructionSite
            => this.StructurePointsMax / 4;

        public sealed override void ClientDeinitialize(IStaticWorldObject gameObject)
        {
            base.ClientDeinitialize(gameObject);

            if (this.currentDisplayedRepairTooltip != null)
            {
                var tooltipDisplayedForObject =
                    ((ConstructionOrRepairRequirementsTooltip)this.currentDisplayedRepairTooltip.Control).WorldObject;
                if (gameObject == tooltipDisplayedForObject)
                {
                    // destroy tooltip
                    this.currentDisplayedRepairTooltip?.Destroy();
                    this.currentDisplayedRepairTooltip = null;
                }
            }

            this.ClientDeinitializeStructure(gameObject);
        }

        public override string ClientGetTitle(IStaticWorldObject worldObject)
        {
            return this.Name;
        }

        public virtual IConstructionStageConfigReadOnly GetStructureActiveConfig(IStaticWorldObject staticWorldObject)
        {
            if (staticWorldObject.ProtoGameObject != this)
            {
                throw new Exception($"{staticWorldObject} is not {this}");
            }

            return this.ConfigRepair;
        }

        public void PrepareProtoSetLinkWithTechNode(TechNode techNode)
        {
            if (this.listedInTechNodes == null)
            {
                if (this.IsAutoUnlocked)
                {
                    Logger.Error(
                        this
                        + " is marked as "
                        + nameof(this.IsAutoUnlocked)
                        + " but the technology is set as the prerequisite: "
                        + techNode);
                }

                this.listedInTechNodes = new List<TechNode>();
            }

            this.listedInTechNodes.AddIfNotContains(techNode);
        }

        public void ServerApplyDamage(IStaticWorldObject worldObject, double damage)
        {
            this.VerifyGameObject(worldObject);

            var publicState = GetPublicState(worldObject);
            var newStructurePoints = (float)(publicState.StructurePointsCurrent - damage);
            bool isDestroyed;

            if (newStructurePoints > 0)
            {
                isDestroyed = false;
            }
            else
            {
                newStructurePoints = 0;
                isDestroyed = true;
            }

            publicState.StructurePointsCurrent = newStructurePoints;

            if (isDestroyed)
            {
                this.ServerOnStaticObjectZeroStructurePoints(null, null, worldObject);
            }
        }

        public virtual void ServerApplyDecay(IStaticWorldObject worldObject, double deltaTime)
        {
            // calculate decay damage
            // damage is proportional to the structure max HP and the decay duration constant
            var damage = this.StructurePointsMax
                         / StructureConstants.StructureDecayDurationSeconds;

            // damage is proportional to the decay system update rate
            damage *= StructureConstants.StructureDecaySystemUpdateIntervalSeconds;

            if (damage < 0.01)
            {
                // clamp minimal damage
                damage = 0.01;
            }

            // apply decay damage
            this.ServerApplyDamage(worldObject, damage);
        }

        public virtual void ServerOnBuilt(IStaticWorldObject structure, ICharacter byCharacter)
        {
        }

        public virtual void SharedCreatePhysicsConstructionBlueprint(IPhysicsBody physicsBody)
        {
            foreach (Vector2D tileOffset in this.Layout.TileOffsets)
            {
                physicsBody.AddShapeRectangle(Vector2D.One, tileOffset, CollisionGroups.Default)
                           .AddShapeRectangle(Vector2D.One, tileOffset, CollisionGroups.ClickArea);
            }
        }

        public virtual float SharedGetStructurePointsMax(IStaticWorldObject worldObject)
            => this.StructurePointsMax;

        public bool SharedIsTechUnlocked(ICharacter character, bool allowIfAdmin = true)
        {
            if (allowIfAdmin
                && CreativeModeSystem.SharedIsInCreativeMode(character))
            {
                return true;
            }

            if (this.listedInTechNodes == null
                || this.listedInTechNodes.Count == 0)
            {
                return this.IsAutoUnlocked;
            }

            var techs = character.SharedGetTechnologies();
            foreach (var node in this.listedInTechNodes)
            {
                if (techs.SharedIsNodeUnlocked(node))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual void SharedOnDeconstruction(
            IStaticWorldObject worldObject,
            ICharacter byCharacter,
            float oldStructurePoints,
            float newStructurePoints)
        {
            var destroyPosition = worldObject.TilePosition.ToVector2D()
                                  + worldObject.ProtoStaticWorldObject.SharedGetObjectCenterWorldOffset(worldObject);
            if (Api.IsClient)
            {
                this.ClientPlayDeconstructionSound(destroyPosition, isByCurrentCharacter: true);
                return;
            }

            using (var scopedBy = Api.Shared.GetTempList<ICharacter>())
            {
                Server.World.GetScopedByPlayers(worldObject, scopedBy);
                scopedBy.Remove(byCharacter);
                this.CallClient(scopedBy,
                                _ => _.ClientRemote_PlayDeconstructionSound(destroyPosition));
            }

            newStructurePoints = Math.Max(0, newStructurePoints);
            GetPublicState(worldObject).StructurePointsCurrent = newStructurePoints;

            if (newStructurePoints <= 0)
            {
                this.ServerOnStaticObjectZeroStructurePoints(null, byCharacter, worldObject);
            }
        }

        protected static TProtoStructureCategory GetCategory<TProtoStructureCategory>()
            where TProtoStructureCategory : ProtoStructureCategory, new()
        {
            return Api.GetProtoEntity<TProtoStructureCategory>();
        }

        protected virtual void ClientDeinitializeStructure(IStaticWorldObject gameObject)
        {
        }

        protected override void ClientUpdate(ClientUpdateData data)
        {
            base.ClientUpdate(data);

            if (this.currentDisplayedRepairTooltip != null)
            {
                var tooltipDisplayedForObject =
                    ((ConstructionOrRepairRequirementsTooltip)this.currentDisplayedRepairTooltip.Control).WorldObject;
                if (data.GameObject == tooltipDisplayedForObject
                    && !this.IsConstructionOrRepairRequirementsTooltipShouldBeDisplayed(data.SyncPublicState))
                {
                    // destroy tooltip
                    this.currentDisplayedRepairTooltip?.Destroy();
                    this.currentDisplayedRepairTooltip = null;
                }
            }

            if (this.currentDisplayedRepairTooltip == null
                && this.IsConstructionOrRepairRequirementsTooltipShouldBeDisplayed(data.SyncPublicState))
            {
                // display tooltip
                var worldObject = data.GameObject;
                this.currentDisplayedRepairTooltip =
                    ConstructionOrRepairRequirementsTooltip.CreateAndAttach(worldObject);
            }
        }

        protected virtual bool IsConstructionOrRepairRequirementsTooltipShouldBeDisplayed(TPublicState publicState)
        {
            return ClientComponentObjectInteractionHelper.CurrentMouseOverObject == publicState.GameObject
                   && this.ConfigRepair.IsAllowed
                   && publicState.StructurePointsCurrent < this.StructurePointsMax
                   && ClientHotbarSelectedItemManager.SelectedItem?.ProtoItem is IProtoItemToolToolbox;
        }

        protected abstract void PrepareConstructionConfig(
            ConstructionTileRequirements tileRequirements,
            ConstructionStageConfig build,
            ConstructionStageConfig repair,
            ConstructionUpgradeConfig upgrade,
            out ProtoStructureCategory category);

        protected sealed override void PrepareTileRequirements(ConstructionTileRequirements tileRequirements)
        {
            var configBuild = new ConstructionStageConfig();
            var configRepair = new ConstructionStageConfig();
            var configUpgrade = new ConstructionUpgradeConfig();

            tileRequirements.Add(ConstructionTileRequirements.ValidatorNotRestrictedArea)
                            .Add(ConstructionTileRequirements.ValidatorNoNpcsAround)
                            .Add(LandClaimSystem.ValidatorIsOwnedOrFreeArea)
                            .Add(LandClaimSystem.ValidatorNoRaid);

            this.PrepareConstructionConfig(
                tileRequirements,
                configBuild,
                configRepair,
                configUpgrade,
                out var category);

            this.Category = category
                            ?? throw new Exception(
                                "Structure category is not set during "
                                + nameof(this.PrepareConstructionConfig)
                                + " call");

            foreach (var upgradeEntry in configUpgrade.Entries)
            {
                if (upgradeEntry.RequiredItems.Count > 4)
                {
                    // we don't allow more than 4 items for upgrades (the upgrade UI space is strictly limited)
                    throw new Exception(this.ShortId
                                        + " requires more than 4 items to upgrade to "
                                        + upgradeEntry.ProtoStructure.ShortId
                                        + ". Max 4 items allowed");
                }
            }

            this.ConfigBuild = configBuild;
            this.ConfigRepair = configRepair;
            this.ConfigUpgrade = configUpgrade;
        }

        protected override void ServerInitialize(ServerInitializeData data)
        {
            base.ServerInitialize(data);

            if (data.IsFirstTimeInit)
            {
                StructureDecaySystem.ServerResetDecayTimer(data.PrivateState);
            }
        }

        protected override double SharedCalculateDamageByWeapon(
            WeaponFinalCache weaponCache,
            double damagePreMultiplier,
            IStaticWorldObject targetObject,
            out double obstacleBlockDamageCoef)
        {
            if (weaponCache.ProtoWeapon is ItemNoWeapon)
            {
                // no damage with hands
                obstacleBlockDamageCoef = 1;

                if (IsClient)
                {
                    NotificationSystem.ClientShowNotification(NotificationUseWeaponOrTool,
                                                              icon: this.Icon);
                }

                return 0;
            }

            return base.SharedCalculateDamageByWeapon(weaponCache,
                                                      damagePreMultiplier,
                                                      targetObject,
                                                      out obstacleBlockDamageCoef);
        }

        private void ClientPlayDeconstructionSound(Vector2D worldPosition, bool isByCurrentCharacter)
        {
            var pitch = RandomHelper.Range(0.95f, 1.05f);

            if (isByCurrentCharacter)
            {
                MaterialHitsSoundPresets.Melee.PlaySound(
                    this.ObjectSoundMaterial,
                    volume: SoundConstants.VolumeHit,
                    pitch: pitch);
            }
            else
            {
                MaterialHitsSoundPresets.Melee.PlaySound(
                    this.ObjectSoundMaterial,
                    worldPosition: worldPosition,
                    volume: SoundConstants.VolumeHit,
                    pitch: pitch);
            }
        }

        [RemoteCallSettings(DeliveryMode.ReliableUnordered)]
        private void ClientRemote_PlayDeconstructionSound(Vector2D worldPosition)
        {
            this.ClientPlayDeconstructionSound(worldPosition, isByCurrentCharacter: false);
        }
    }

    public abstract class ProtoObjectStructure
        : ProtoObjectStructure
            <StructurePrivateState, StaticObjectPublicState, StaticObjectClientState>
    {
    }
}