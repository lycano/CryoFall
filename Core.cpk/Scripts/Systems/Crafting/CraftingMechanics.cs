﻿namespace AtomicTorch.CBND.CoreMod.Systems.Crafting
{
    using System;
    using System.Linq;
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.Systems.Creative;
    using AtomicTorch.CBND.CoreMod.Systems.Notifications;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Items;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Logging;
    using AtomicTorch.CBND.GameApi.Scripting;
    using JetBrains.Annotations;

    /// <summary>
    /// Mechanic for items crafting (crafting queue manipulation and processing).
    /// </summary>
    public static class CraftingMechanics
    {
        private static readonly ILogger Logger = Api.Logger;

        public delegate void RecipeCraftedDelegate(CraftingQueueItem craftingQueueItem);

        public static event RecipeCraftedDelegate ServerNonManufacturingRecipeCrafted;

        /// <summary>
        /// Return all input items to player - or drop on the ground
        /// </summary>
        /// <param name="character"></param>
        /// <param name="craftingQueue"></param>
        public static void ServerCancelCraftingQueue(ICharacter character)
        {
            var craftingQueue = PlayerCharacter.GetPrivateState(character)
                                               .CraftingQueue;
            if (craftingQueue.QueueItems.Count == 0)
            {
                return;
            }

            IItemsContainer groundContainer = null;
            var queueItems = craftingQueue.QueueItems.ToList();
            foreach (var item in queueItems)
            {
                ServerCancelCraftingQueueItem(character,
                                              item,
                                              craftingQueue,
                                              ref groundContainer);
            }

            Logger.Info("Crafting queue successfully cancelled", character);
        }

        /// <summary>
        /// Cancel crafting queue item.
        /// </summary>
        public static void ServerCancelCraftingQueueItem(
            ICharacter character,
            CraftingQueueItem item)
        {
            var craftingQueue = character.GetPrivateState<PlayerCharacterPrivateState>()
                                         .CraftingQueue;

            IItemsContainer groundContainer = null;
            ServerCancelCraftingQueueItem(character,
                                          item,
                                          craftingQueue,
                                          ref groundContainer);

            if (groundContainer != null)
            {
                NotificationSystem.ServerSendNotificationNoSpaceInInventoryItemsDroppedToGround(character);
            }
        }

        /// <summary>
        /// Enqueue crafting selected recipe.
        /// </summary>
        /// <param name="craftingQueue">Crafting queue instance.</param>
        /// <param name="recipe">Recipe instance.</param>
        /// <param name="countToCraft">Count to craft - must be greater than zero.</param>
        public static void ServerStartCrafting(
            [CanBeNull] IStaticWorldObject station,
            [CanBeNull] ICharacter character,
            [NotNull] CraftingQueue craftingQueue,
            [NotNull] Recipe recipe,
            ushort countToCraft,
            ushort? maxQueueSize = null)
        {
            var characterOrStation = (IWorldObject)station ?? character;
            if (characterOrStation == null)
            {
                throw new NullReferenceException("Character AND station cannot be null simultaneously");
            }

            if (countToCraft == 0)
            {
                throw new Exception("Are you really want to craft zero items?");
            }

            var isAdminMode = character != null
                              && CreativeModeSystem.SharedIsInCreativeMode(character);

            if (!isAdminMode
                && recipe.RecipeType != RecipeType.ManufacturingByproduct
                && !recipe.CanBeCrafted(
                    characterOrStation,
                    craftingQueue,
                    countToCraft: recipe.RecipeType == RecipeType.Manufacturing
                                      ? (ushort)1
                                      : countToCraft))
            {
                Logger.Error($"Recipe cannot be crafted - check failed: {recipe} at {characterOrStation}.");
                return;
            }

            var queueCount = craftingQueue.QueueItems.Count;
            if (queueCount > 0)
            {
                foreach (var existingQueueItem in craftingQueue.QueueItems)
                {
                    if (!existingQueueItem.CanCombineWith(recipe))
                    {
                        continue;
                    }

                    // try to increase the count to craft
                    var lastQueueItemCountBefore = existingQueueItem.CountToCraftRemains;

                    var maxCountToCraft = ushort.MaxValue;
                    var isManufacturingRecipe = recipe.RecipeType != RecipeType.Hand
                                                && recipe.RecipeType != RecipeType.StationCrafting;

                    if (!isManufacturingRecipe)
                    {
                        maxCountToCraft = recipe.OutputItems.Items[0].ProtoItem.MaxItemsPerStack;
                    }

                    var originalRequestedCountToCraft = countToCraft;
                    var lastQueueItemCountNew =
                        (ushort)Math.Min(maxCountToCraft, lastQueueItemCountBefore + countToCraft);
                    var canAddCount = lastQueueItemCountNew - lastQueueItemCountBefore;

                    if (canAddCount > 0)
                    {
                        existingQueueItem.CountToCraftRemains = lastQueueItemCountNew;
                        Logger.Info(
                            $"Recipe count extended for crafting: {recipe} at {characterOrStation}: from x{lastQueueItemCountBefore} to x{countToCraft}");

                        ServerDestroyInputItems(craftingQueue,
                                                recipe,
                                                countToCraft: (ushort)canAddCount,
                                                isAdminMode);

                        if (isManufacturingRecipe)
                        {
                            return;
                        }

                        var remainingCountToCraft = originalRequestedCountToCraft
                                                    - canAddCount;
                        if (remainingCountToCraft <= 0)
                        {
                            // the last queue item took all the requested items count to craft
                            return;
                        }

                        // last queue item cannot accomodate all the requested count to craft
                        // let's try to add to previous queue item or a new queue item
                        countToCraft = (ushort)remainingCountToCraft;
                    }
                }
            }

            if (maxQueueSize.HasValue
                && craftingQueue.QueueItems.Count + 1 >= maxQueueSize.Value)
            {
                Logger.Info(
                    $"Recipe cannot be queue for crafting due to max queue size limitation: {recipe} at {characterOrStation} with max queue size {maxQueueSize.Value}.");
                return;
            }

            var queueItem = new CraftingQueueItem(recipe, countToCraft, craftingQueue.ServerLastQueueItemLocalId++);
            ServerDestroyInputItems(craftingQueue,
                                    recipe,
                                    countToCraft: queueItem.CountToCraftRemains,
                                    isAdminMode);

            craftingQueue.QueueItems.Add(queueItem);
            if (craftingQueue.QueueItems.Count == 1)
            {
                // the added recipe is first in queue - so we will craft it right now
                craftingQueue.SetDurationFromCurrentRecipe();
            }

            Logger.Info($"Recipe queued for crafting: {recipe} at {characterOrStation}.");
        }

        /// <summary>
        /// Update crafting queue. It will progress current crafting queue item.
        /// </summary>
        /// <param name="craftingQueue">Crafting queue item.</param>
        /// <param name="deltaTime">Delta time in seconds to progress on.</param>
        public static void ServerUpdate(CraftingQueue craftingQueue, double deltaTime)
        {
            do
            {
                var queue = craftingQueue.QueueItems;
                if (queue.Count == 0)
                {
                    // the queue is empty
                    return;
                }

                var queueItem = queue[0];
                ServerProgressQueueItem(craftingQueue, queueItem, ref deltaTime);
            }
            while (deltaTime > 0
                   && !craftingQueue.IsContainerOutputFull);
        }

        private static void ServerCancelCraftingQueueItem(
            ICharacter character,
            CraftingQueueItem item,
            CraftingQueue craftingQueue,
            ref IItemsContainer groundContainer)
        {
            if (!item.Recipe.IsCancellable)
            {
                Logger.Info("Crafting queue item is not cancellable: " + item, character);
                return;
            }

            var index = craftingQueue.QueueItems.IndexOf(item);
            if (index < 0)
            {
                throw new Exception("Doesn't have the crafting queue item: " + item);
            }

            ServerReturnCharacterCraftingQueueItemInputItems(character, item, ref groundContainer);
            craftingQueue.QueueItems.RemoveAt(index);

            if (index == 0)
            {
                // first queue item was removed - reset crafting duration
                craftingQueue.SetDurationFromCurrentRecipe();
            }

            Logger.Info("Crafting queue item successfully cancelled: " + item, character);
        }

        private static void ServerDestroyInputItems(
            Recipe recipe,
            IItemsContainerProvider inputContainers,
            ushort countToCraft,
            bool isAdminMode)
        {
            var itemsService = Api.Server.Items;
            foreach (var inputItem in recipe.InputItems)
            {
                var countToDestroy = (ushort)(inputItem.Count * countToCraft);
                var inputItemType = inputItem.ProtoItem;

                if (isAdminMode)
                {
                    // destroy only available count
                    var availableCount =
                        inputContainers.Sum(c => c.Items.Where(i => i.ProtoItem == inputItemType).Sum(i => i.Count));
                    if (availableCount == 0)
                    {
                        // nothing to destroy
                        continue;
                    }

                    if (countToDestroy > availableCount)
                    {
                        // limit count to destroy
                        countToDestroy = (ushort)availableCount;
                    }
                }

                itemsService.DestroyItemsOfType(
                    inputContainers,
                    inputItemType,
                    countToDestroy,
                    out _);
            }
        }

        private static void ServerDestroyInputItems(
            CraftingQueue craftingQueue,
            Recipe recipe,
            ushort countToCraft,
            bool isAdminMode)
        {
            switch (recipe.RecipeType)
            {
                case RecipeType.Manufacturing:
                    // auto-manufacturers: do not destroy input items until crafting completed
                    break;

                case RecipeType.ManufacturingByproduct:
                    // station by-products doesn't require input items - it checks only for proper item prototype upon start
                    break;

                default:
                    // other recipes should instantly destroy the input items
                    ServerDestroyInputItems(recipe,
                                            new AggregatedItemsContainers(craftingQueue.InputContainersArray),
                                            countToCraft,
                                            isAdminMode);
                    break;
            }
        }

        private static void ServerProgressQueueItem(
            CraftingQueue craftingQueue,
            CraftingQueueItem queueItem,
            ref double deltaTime)
        {
            if (craftingQueue.TimeRemainsToComplete > deltaTime)
            {
                // consume deltaTime
                craftingQueue.TimeRemainsToComplete -= deltaTime;
                deltaTime = 0;
                // need more time to complete crafting
                return;
            }

            // crafting of item completed
            deltaTime -= craftingQueue.TimeRemainsToComplete;
            craftingQueue.TimeRemainsToComplete = 0;

            if (!ServerTryCreateOutputItems(craftingQueue, queueItem))
            {
                // need more space in output container
                return;
            }

            Logger.Info($"Crafting of {queueItem} completed.");
            var recipe = queueItem.Recipe;
            if (recipe.RecipeType == RecipeType.Manufacturing)
            {
                // auto-manufacturers: destroy input items when crafting completed
                ServerDestroyInputItems(
                    recipe,
                    new AggregatedItemsContainers(craftingQueue.InputContainersArray),
                    countToCraft: 1,
                    isAdminMode: false);
            }

            Api.SafeInvoke(() => ServerNonManufacturingRecipeCrafted?.Invoke(queueItem));

            if (recipe.RecipeType == RecipeType.Manufacturing
                || recipe.RecipeType == RecipeType.ManufacturingByproduct)
            {
                // do not reduce count to craft as this is a manufacturing recipe
                var station = (IWorldObject)craftingQueue.GameObject;

                if (recipe.RecipeType == RecipeType.Manufacturing
                    && !recipe.CanBeCrafted(
                        station,
                        craftingQueue,
                        countToCraft: 1))
                {
                    Logger.Info(
                        $"Manufacturing recipe cannot be crafted anymore - check failed: {recipe} at {station}.");
                    queueItem.CountToCraftRemains = 0;
                }
            }
            else // if this is a non-manufacturing recipe
            {
                queueItem.CountToCraftRemains--;
            }

            if (queueItem.CountToCraftRemains == 0)
            {
                // remove from queue
                craftingQueue.QueueItems.Remove(queueItem);
            }

            craftingQueue.SetDurationFromCurrentRecipe();
        }

        private static void ServerReturnCharacterCraftingQueueItemInputItems(
            ICharacter character,
            CraftingQueueItem queueItem,
            ref IItemsContainer groundContainer)
        {
            foreach (var inputItem in queueItem.Recipe.InputItems)
            {
                var countToSpawn = queueItem.CountToCraftRemains * (uint)inputItem.Count;
                PlayerCharacter.ServerTrySpawnItemToCharacterOrGround(character,
                                                                      inputItem.ProtoItem,
                                                                      countToSpawn,
                                                                      ref groundContainer);
            }
        }

        private static bool ServerTryCreateOutputItems(CraftingQueue craftingQueue, CraftingQueueItem queueItem)
        {
            if (craftingQueue.IsContainerOutputFull
                && craftingQueue.ContainerOutputLastStateHash == craftingQueue.ContainerOutput.StateHash)
            {
                return false;
            }

            var recipe = queueItem.Recipe;
            var result = recipe.OutputItems.TrySpawnToContainer(craftingQueue.ContainerOutput);
            if (!result.IsEverythingCreated)
            {
                // cannot create items at specified container
                result.Rollback();
                craftingQueue.IsContainerOutputFull = true;
                craftingQueue.ContainerOutputLastStateHash = craftingQueue.ContainerOutput.StateHash;
                return false;
            }

            // something is created, assume crafting success
            craftingQueue.IsContainerOutputFull = false;
            craftingQueue.ContainerOutputLastStateHash = craftingQueue.ContainerOutput.StateHash;

            if (recipe is Recipe.RecipeForManufacturing recipeManufacturing)
            {
                recipeManufacturing.ServerOnManufacturingCompleted(
                    ((ManufacturingCraftingQueue)craftingQueue).WorldObject,
                    craftingQueue);
            }

            if (craftingQueue is CharacterCraftingQueue characterCraftingQueue)
            {
                NotificationSystem.ServerSendItemsNotification(
                    characterCraftingQueue.Character,
                    result);
            }

            return true;
        }
    }
}