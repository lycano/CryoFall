﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Misc
{
    using System;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.Items.Equipment;
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.Items.Implants;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.Systems.Construction;
    using AtomicTorch.CBND.CoreMod.Systems.Creative;
    using AtomicTorch.CBND.CoreMod.Systems.InteractionChecker;
    using AtomicTorch.CBND.CoreMod.Systems.Notifications;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects.MedicalStations;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects.MedicalStations.Data;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Items;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    public class ObjectMedicalStation : ProtoObjectStructure, IInteractableProtoStaticWorldObject
    {
        private static readonly Lazy<ItemVialBiomaterial> ProtoItemBiomaterialVial
            = new Lazy<ItemVialBiomaterial>(GetProtoEntity<ItemVialBiomaterial>);

        private static ObjectMedicalStation instance;

        public override string Description =>
            "Special automated medical station that can be used to install or remove all types of cybernetic implants.";

        public bool IsAutoEnterPrivateScopeOnInteraction => true;

        public override string Name => "Medical station";

        public override ObjectSoundMaterial ObjectSoundMaterial => ObjectSoundMaterial.Metal;

        public override double ObstacleBlockDamageCoef => 1;

        public override float StructurePointsMax => 500;

        public static void ClientInstall(IItem itemToInstall, byte slotId)
        {
            instance.CallServer(_ => _.ServerRemote_Install(itemToInstall, slotId));
        }

        public static void ClientUninstall(byte slotId)
        {
            instance.CallServer(_ => _.ServerRemote_Uninstall(slotId));
        }

        BaseUserControlWithWindow IInteractableProtoStaticWorldObject.ClientOpenUI(IStaticWorldObject worldObject)
        {
            return this.ClientOpenUI(new ClientObjectData(worldObject));
        }

        void IInteractableProtoStaticWorldObject.ServerOnClientInteract(ICharacter who, IStaticWorldObject worldObject)
        {
            // do nothing
        }

        void IInteractableProtoStaticWorldObject.ServerOnMenuClosed(ICharacter who, IStaticWorldObject worldObject)
        {
            // do nothing
        }

        protected override void ClientInteractStart(ClientObjectData data)
        {
            InteractableStaticWorldObjectHelper.ClientStartInteract(data.GameObject);
        }

        protected BaseUserControlWithWindow ClientOpenUI(ClientObjectData data)
        {
            return WindowMedicalStation.Open(
                new ViewModelWindowMedicalStation());
        }

        protected override void ClientSetupRenderer(IComponentSpriteRenderer renderer)
        {
            base.ClientSetupRenderer(renderer);
            renderer.DrawOrderOffsetY = 1.8;
        }

        protected override void CreateLayout(StaticObjectLayout layout)
        {
            layout.Setup("###",
                         "###",
                         "###");
        }

        protected override void PrepareConstructionConfig(
            ConstructionTileRequirements tileRequirements,
            ConstructionStageConfig build,
            ConstructionStageConfig repair,
            ConstructionUpgradeConfig upgrade,
            out ProtoStructureCategory category)
        {
            category = GetCategory<StructureCategoryOther>();

            // TODO: set proper requirements
            build.StagesCount = 10;
            build.StageDurationSeconds = BuildDuration.Short;
            build.AddStageRequiredItem<ItemIngotSteel>(count: 25);
            build.AddStageRequiredItem<ItemIngotCopper>(count: 25);
            build.AddStageRequiredItem<ItemIngotLithium>(count: 5);
            build.AddStageRequiredItem<ItemPlastic>(count: 5);
            build.AddStageRequiredItem<ItemComponentsHighTech>(count: 2);

            repair.StagesCount = 10;
            repair.StageDurationSeconds = BuildDuration.Short;
            repair.AddStageRequiredItem<ItemIngotSteel>(count: 10);
            repair.AddStageRequiredItem<ItemIngotCopper>(count: 10);
        }

        protected override void PrepareProtoStaticWorldObject()
        {
            base.PrepareProtoStaticWorldObject();
            instance = this;
        }

        protected override void SharedCreatePhysics(
            CreatePhysicsData data)
        {
            data.PhysicsBody
                .AddShapeRectangle((2.6, 1.9), (0.2, 0.1))
                .AddShapeRectangle((2.6, 2.1), (0.2, 0.1), CollisionGroups.HitboxMelee)
                .AddShapeRectangle((2.6, 2.3), (0.2, 0.1), CollisionGroups.HitboxRanged)
                .AddShapeRectangle((2.6, 2.5), (0.2, 0.1), CollisionGroups.ClickArea);
        }

        private void ServerRemote_Install(IItem itemToInstall, byte slotId)
        {
            var character = ServerRemoteContext.Character;
            var worldObject = InteractionCheckerSystem.GetCurrentInteraction(character);
            this.VerifyGameObject((IStaticWorldObject)worldObject);

            var containerEquipment = character.SharedGetPlayerContainerEquipment();
            var currentInstalledItem = containerEquipment.GetItemAtSlot(slotId);
            if (currentInstalledItem != null)
            {
                if (currentInstalledItem == itemToInstall)
                {
                    Logger.Info("The implant is already installed");
                    return;
                }

                throw new Exception("Please uninstall installed implant");
            }

            if (itemToInstall.Container.OwnerAsCharacter != character
                || itemToInstall.Container == containerEquipment)
            {
                throw new Exception("The item to install must be in character containers (except equipment)");
            }

            if (!containerEquipment.ProtoItemsContainer.CanAddItem(
                    new CanAddItemContext(containerEquipment,
                                          itemToInstall,
                                          slotId,
                                          byCharacter: null,
                                          isExploratoryCheck: false)))
            {
                throw new Exception("Cannot install implant item there");
            }

            var itemToInstallProto = (IProtoItemEquipmentImplant)itemToInstall.ProtoItem;
            if (itemToInstallProto is ItemImplantBroken)
            {
                throw new Exception("Cannot install broken implant");
            }

            foreach (var equippedItem in containerEquipment.Items)
            {
                if (equippedItem.ProtoItem == itemToInstallProto)
                {
                    throw new Exception("Another implant of this type is already installed: " + itemToInstallProto);
                }
            }

            var biomaterialRequiredAmount = CreativeModeSystem.SharedIsInCreativeMode(character)
                                                ? (ushort)0
                                                : itemToInstallProto.BiomaterialAmountRequiredToInstall;

            if (!character.ContainsItemsOfType(ProtoItemBiomaterialVial.Value,
                                               requiredCount: biomaterialRequiredAmount))
            {
                throw new Exception("Not enough biomaterial vials");
            }

            if (!Server.Items.MoveOrSwapItem(itemToInstall,
                                             containerEquipment,
                                             out _,
                                             slotId: slotId))
            {
                throw new Exception("Unknown error - cannot move implant item to the player equipment");
            }

            if (biomaterialRequiredAmount > 0)
            {
                // destroy vials
                Server.Items.DestroyItemsOfType(character,
                                                ProtoItemBiomaterialVial.Value,
                                                countToDestroy: biomaterialRequiredAmount,
                                                out _);

                NotificationSystem.ServerSendItemsNotification(
                    character,
                    ProtoItemBiomaterialVial.Value,
                    -biomaterialRequiredAmount);
            }

            Logger.Info("Implant installed: " + itemToInstall);
        }

        private void ServerRemote_Uninstall(byte slotId)
        {
            var character = ServerRemoteContext.Character;
            var worldObject = InteractionCheckerSystem.GetCurrentInteraction(character);
            this.VerifyGameObject((IStaticWorldObject)worldObject);

            var itemToUninstall = character.SharedGetPlayerContainerEquipment()
                                           .GetItemAtSlot(slotId);
            if (itemToUninstall == null)
            {
                throw new Exception("No implant installed");
            }

            var itemToUninstallProto = (IProtoItemEquipmentImplant)itemToUninstall.ProtoItem;
            var biomaterialRequiredAmount = CreativeModeSystem.SharedIsInCreativeMode(character)
                                                ? (ushort)0
                                                : itemToUninstallProto.BiomaterialAmountRequiredToUninstall;

            if (!character.ContainsItemsOfType(ProtoItemBiomaterialVial.Value,
                                               requiredCount: biomaterialRequiredAmount)
                && !CreativeModeSystem.SharedIsInCreativeMode(character))
            {
                throw new Exception("Not enough biomaterial vials");
            }

            // move to inventory
            if (!Server.Items.MoveOrSwapItem(itemToUninstall,
                                             character.SharedGetPlayerContainerInventory(),
                                             out _))
            {
                NotificationSystem.ServerSendNotificationNoSpaceInInventory(character);
                return;
            }

            if (biomaterialRequiredAmount > 0)
            {
                // destroy vials
                Server.Items.DestroyItemsOfType(character,
                                                ProtoItemBiomaterialVial.Value,
                                                countToDestroy: biomaterialRequiredAmount,
                                                out _);

                NotificationSystem.ServerSendItemsNotification(
                    character,
                    ProtoItemBiomaterialVial.Value,
                    -biomaterialRequiredAmount);
            }

            if (itemToUninstallProto is ItemImplantBroken)
            {
                // broken implant destroys on uninstall
                Server.Items.DestroyItem(itemToUninstall);
                NotificationSystem.ServerSendItemsNotification(
                    character,
                    itemToUninstallProto,
                    -1);
            }

            Logger.Info("Implant uninstalled: " + itemToUninstall);
        }
    }
}