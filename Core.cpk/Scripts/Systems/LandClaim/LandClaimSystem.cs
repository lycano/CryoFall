﻿namespace AtomicTorch.CBND.CoreMod.Systems.LandClaim
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AtomicTorch.CBND.CoreMod.Bootstrappers;
    using AtomicTorch.CBND.CoreMod.Perks;
    using AtomicTorch.CBND.CoreMod.StaticObjects;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Deposits;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.ConstructionSite;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.LandClaim;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Walls;
    using AtomicTorch.CBND.CoreMod.Systems.Creative;
    using AtomicTorch.CBND.CoreMod.Systems.Deconstruction;
    using AtomicTorch.CBND.CoreMod.Systems.Notifications;
    using AtomicTorch.CBND.CoreMod.Systems.ServerTimers;
    using AtomicTorch.CBND.CoreMod.Systems.StructureDecaySystem;
    using AtomicTorch.CBND.CoreMod.Systems.WorldObjectOwners;
    using AtomicTorch.CBND.GameApi.Data;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Logic;
    using AtomicTorch.CBND.GameApi.Data.State.NetSync;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.CBND.GameApi.ServicesClient;
    using AtomicTorch.CBND.GameApi.ServicesServer;
    using AtomicTorch.GameEngine.Common.DataStructures;
    using AtomicTorch.GameEngine.Common.Extensions;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class LandClaimSystem : ProtoSystem<LandClaimSystem>
    {
        public const string ErrorCannotBuild_AreaIsClaimedOrTooCloseToClaimed =
            "The area is claimed by another player, or it's too close to someone else's land claim.";

        public const string ErrorCannotBuild_IntersectingWithAnotherLandClaim =
            "Intersecting with another land claim area";

        public const string ErrorCannotBuild_LandClaimAmountLimitExceeded =
            @"You've used up your allotted number of land claims.
              [br]You can increase that number by researching new technologies.";

        public const string ErrorCannotBuild_RaidUnderWay =
            @"Raid under way.
              [br]You cannot build new structures while your base is under attack.";

        /// <summary>
        /// Determines how many protected cells should be around the land claim area to prevent
        /// building in this neutral (grace) area by other players.
        /// Please note it's still possible to claim this (grace) area by another land claim.
        /// </summary>
        public const int LandClaimAreaGracePaddingSize = 5;

        public const double RaidCooldownDurationSeconds = 5 * 60; // 5 minutes

        private static ILogicObject serverLandClaimManagerInstance;

        private static NetworkSyncList<ILogicObject> sharedLandClaimAreas;

        public static ConstructionTileRequirements.Validator ValidatorIsOwnedOrFreeArea
            = new ConstructionTileRequirements.Validator(
                ErrorCannotBuild_AreaIsClaimedOrTooCloseToClaimed,
                context =>
                {
                    var forCharacter = context.CharacterBuilder;
                    if (forCharacter == null)
                    {
                        return true;
                    }

                    if (CreativeModeSystem.SharedIsInCreativeMode(forCharacter))
                    {
                        return true;
                    }

                    var position = context.Tile.Position;

                    var noAreasThere = true;
                    foreach (var area in sharedLandClaimAreas)
                    {
                        var areaBoundsDirect = SharedGetLandClaimAreaBounds(area);
                        var areaBoundsWithPadding = areaBoundsDirect.Inflate(LandClaimAreaGracePaddingSize,
                                                                             LandClaimAreaGracePaddingSize);

                        if (!areaBoundsWithPadding.Contains(position))
                        {
                            // there is no area (even with the padding/grace area)
                            continue;
                        }

                        if (areaBoundsDirect.Contains(position))
                        {
                            // there is a direct area (not padding/grace area)
                            if (SharedIsOwnedArea(area, forCharacter))
                            {
                                // player owns the area
                                return true;
                            }

                            noAreasThere = false;
                        }
                        else
                        {
                            // there is no direct area (only the padding/grace area)
                            if (!SharedIsOwnedArea(area, forCharacter))
                            {
                                // the grace/padding area of another player's land claim found
                                noAreasThere = false;
                            }
                        }
                    }

                    // if we've come to here here and there are any areas it means the player doesn't own any of them
                    return noAreasThere;
                });

        public static ConstructionTileRequirements.Validator ValidatorNoRaid
            = new ConstructionTileRequirements.Validator(
                ErrorCannotBuild_RaidUnderWay,
                context =>
                {
                    var forCharacter = context.CharacterBuilder;
                    if (forCharacter == null)
                    {
                        return true;
                    }

                    if (CreativeModeSystem.SharedIsInCreativeMode(forCharacter))
                    {
                        return true;
                    }

                    var area = SharedGetAreaAtPosition(context.Tile.Position);
                    if (area == null)
                    {
                        // no area there
                        return true;
                    }

                    if (IsServer
                        || ClientIsOwnedArea(area))
                    {
                        var isUnderRaid = SharedIsOwnedAreaUnderRaid(area);
                        return !isUnderRaid;
                    }

                    return true;
                });

        public static readonly ConstructionTileRequirements.Validator ValidatorNewLandClaimNoLandClaimIntersections
            = new ConstructionTileRequirements.Validator(
                ErrorCannotBuild_IntersectingWithAnotherLandClaim,
                context =>
                {
                    var forCharacter = context.CharacterBuilder;
                    if (forCharacter == null)
                    {
                        return true;
                    }

                    if (CreativeModeSystem.SharedIsInCreativeMode(forCharacter))
                    {
                        return true;
                    }

                    var protoObjectLandClaim = (IProtoObjectLandClaim)context.ProtoStaticObjectToBuild;
                    if (context.TileOffset
                        != SharedCalculateLandClaimObjectCenterTilePosition(Vector2Ushort.Zero, protoObjectLandClaim))
                    {
                        // we don't check offset tiles
                        // as the land claim area calculated from the center tile of the land claim object
                        return true;
                    }

                    var centerTilePosition = context.Tile.Position;
                    return SharedCheckCanPlaceOrUpgradeLandClaimThere(protoObjectLandClaim,
                                                                      centerTilePosition,
                                                                      forCharacter);
                });

        public static ConstructionTileRequirements.Validator ValidatorCheckCharacterLandClaimAmountLimit
            = new ConstructionTileRequirements.Validator(
                ErrorCannotBuild_LandClaimAmountLimitExceeded,
                context =>
                {
                    var forCharacter = context.CharacterBuilder;
                    if (forCharacter == null)
                    {
                        return true;
                    }

                    if (context.TileOffset != Vector2Int.Zero)
                    {
                        return true;
                    }

                    if (CreativeModeSystem.SharedIsInCreativeMode(forCharacter))
                    {
                        return true;
                    }

                    // calculate max number of land claims
                    var maxNumber = 0;
                    // TODO: redone this (the max number should be derived from the character cache as a stat or something like that)
                    if (Api.GetProtoEntity<PerkIncreaseLandClaimLimitT1>()
                           .SharedIsPerkUnlocked(forCharacter))
                    {
                        maxNumber++;
                    }

                    if (Api.GetProtoEntity<PerkIncreaseLandClaimLimitT2>()
                           .SharedIsPerkUnlocked(forCharacter))
                    {
                        maxNumber++;
                    }

                    if (Api.GetProtoEntity<PerkIncreaseLandClaimLimitT3>()
                           .SharedIsPerkUnlocked(forCharacter))
                    {
                        maxNumber++;
                    }

                    if (Api.GetProtoEntity<PerkIncreaseLandClaimLimitT4>()
                           .SharedIsPerkUnlocked(forCharacter))
                    {
                        maxNumber++;
                    }

                    // calculate current number of land claims
                    var currentNumber = 0;
                    foreach (var area in sharedLandClaimAreas)
                    {
                        if (IsClient && !area.ClientHasPrivateState)
                        {
                            continue;
                        }

                        var privateState = LandClaimArea.GetPrivateState(area);
                        if (privateState.LandClaimFounder == forCharacter.Name)
                        {
                            currentNumber++;
                        }
                    }

                    return maxNumber > currentNumber; // ok only in case when the limit is not exceeded
                });

        public static ConstructionTileRequirements.Validator ValidatorCheckCharacterLandClaimDepositClaimLimit
            = new ConstructionTileRequirements.Validator(
                "You need to research Xenogeology (Tier 3) to claim the Oil/Li deposits or to place a land claim close to it.",
                context =>
                {
                    var forCharacter = context.CharacterBuilder;
                    if (forCharacter == null)
                    {
                        return true;
                    }

                    if (context.TileOffset != Vector2Int.Zero)
                    {
                        return true;
                    }

                    if (CreativeModeSystem.SharedIsInCreativeMode(forCharacter))
                    {
                        return true;
                    }

                    if (Api.GetProtoEntity<PerkClaimDeposits>()
                           .SharedIsPerkUnlocked(forCharacter))
                    {
                        // has perk unlocked so no need to check whether player trying to claim a deposit
                        return true;
                    }

                    // doesn't have Xenogeology unlocked
                    // check whether there are any deposits nearby
                    var bounds = SharedCalculateLandClaimAreaBounds(
                        centerTilePosition: (context.Tile.Position
                                             + context.ProtoStaticObjectToBuild.Layout.Center.ToVector2Int())
                        .ToVector2Ushort(),
                        size: (ushort)(MaxLandClaimSize.Value
                                       + LandClaimAreaGracePaddingSize * 2));

                    var worldDeposits = IsClient
                                            ? ClientWorld.GetStaticWorldObjectsOfProto<IProtoObjectDeposit>()
                                            : ServerWorld.GetStaticWorldObjectsOfProto<IProtoObjectDeposit>();

                    foreach (var staticWorldObject in worldDeposits)
                    {
                        if (bounds.Contains(staticWorldObject.TilePosition))
                        {
                            // found a deposit in bounds of the future land claim
                            return false;
                        }
                    }

                    return true;
                });

        public static readonly Lazy<ushort> MaxLandClaimSize
            = new Lazy<ushort>(() => Api.FindProtoEntities<IProtoObjectLandClaim>()
                                        .Max(l => l.LandClaimSize));

        protected internal static readonly IWorldClientService ClientWorld
            = IsClient ? Client.World : null;

        private static readonly IWorldServerService ServerWorld
            = IsServer ? Server.World : null;

        public override string Name => "Land claim system";

        public static bool ClientIsOwnedArea(ILogicObject area)
        {
            if (area == null)
            {
                throw new ArgumentNullException(nameof(area));
            }

            if (!(area.ProtoLogicObject is LandClaimArea))
            {
                throw new Exception($"{area} is not a {nameof(LandClaimArea)}");
            }

            return area.ClientHasPrivateState;
        }

        public static async void ClientSetAreaOwners(ILogicObject area, List<string> newOwners)
        {
            var errorMessage = await Instance.CallServer(
                                   _ => Instance.ServerRemote_SetAreaOwners(area, newOwners));
            if (errorMessage != null)
            {
                NotificationSystem.ClientShowNotification(
                    title: null,
                    message: errorMessage,
                    color: NotificationColor.Bad);
            }
        }

        public static IEnumerable<ILogicObject> ServerEnumerateAllAreas()
        {
            return sharedLandClaimAreas;
        }

        public static ILogicObject ServerGetLandClaimArea(IStaticWorldObject landClaimStructure)
        {
            return landClaimStructure.GetPublicState<ObjectLandClaimPublicState>()
                                     .LandClaimAreaObject;
        }

        public static bool ServerIsObjectInsideOwnedOfFreeArea(
            IStaticWorldObject gameObject,
            ICharacter who)
        {
            foreach (var tileOffset in gameObject.ProtoStaticWorldObject.Layout.TileOffsets)
            {
                var area = SharedGetAreaAtPosition(gameObject.TilePosition.AddAndClamp(tileOffset));
                if (area == null)
                {
                    // no area in this position
                    continue;
                }

                if (ServerIsOwnedArea(area, who))
                {
                    // player owns this area
                    continue;
                }

                // player don't own this area
                return false;
            }

            return true;
        }

        public static bool ServerIsOwnedArea(ILogicObject area, ICharacter character)
        {
            if (area == null)
            {
                Logger.Warning(nameof(ServerIsOwnedArea) + " - argument area is null");
                return false;
            }

            var privateState = LandClaimArea.GetPrivateState(area);
            return privateState.LandOwners
                               .Contains(character.Name);
        }

        public static void ServerNotifyCannotInteractNotOwner(ICharacter character, IStaticWorldObject worldObject)
        {
            Instance.CallClient(character, _ => _.ClientRemote_OnCannotInteractNotOwner(worldObject));
        }

        public static void ServerOnObjectLandClaimBuilt(
            ICharacter byCharacter,
            IStaticWorldObject landClaimStructure)
        {
            if (!(landClaimStructure?.ProtoStaticWorldObject
                      is IProtoObjectLandClaim))
            {
                throw new Exception("Not a land claim structure: " + landClaimStructure);
            }

            // create new area for this land claim structure
            var area = Api.Server.World.CreateLogicObject<LandClaimArea>();
            var areaPrivateState = LandClaimArea.GetPrivateState(area);
            var areaPublicState = LandClaimArea.GetPublicState(area);
            var founderName = byCharacter.Name;

            // setup it
            areaPrivateState.ServerLandClaimWorldObject = landClaimStructure;
            areaPrivateState.LandClaimFounder = founderName;
            areaPrivateState.LandOwners = new NetworkSyncList<string>()
            {
                founderName
            };

            areaPublicState.Title = founderName;
            areaPublicState.SetupAreaProperties(areaPrivateState);

            // set this area to the structure public state
            landClaimStructure.GetPublicState<ObjectLandClaimPublicState>()
                              .LandClaimAreaObject = area;

            ServerOnAddLandOwner(area, byCharacter, notify: false);

            Logger.Important("Land claim area added: " + area);
        }

        public static void ServerOnObjectLandClaimDestroyed(
            IStaticWorldObject landClaimStructure)
        {
            if (!(landClaimStructure?.ProtoStaticWorldObject
                      is IProtoObjectLandClaim))
            {
                throw new Exception("Not a land claim structure: " + landClaimStructure);
            }

            var area = ServerGetLandClaimArea(landClaimStructure);

            if (area == null)
            {
                // area was already released (upgrade?)
                return;
            }

            var areaBounds = SharedGetLandClaimAreaBounds(area);

            ServerWorld.DestroyObject(area);

            Logger.Important("Land claim area removed: " + area);

            // reset decay for all the structures in the destroyed land claim area
            for (var x = (ushort)areaBounds.X; x < areaBounds.X + areaBounds.Width; x++)
            for (var y = (ushort)areaBounds.Y; y < areaBounds.Y + areaBounds.Height; y++)
            {
                var staticObjects = ServerWorld.GetStaticObjects(new Vector2Ushort(x, y));
                foreach (var worldObject in staticObjects)
                {
                    if (worldObject.ProtoStaticWorldObject is IProtoObjectStructure)
                    {
                        StructureDecaySystem.ServerResetDecayTimer(
                            worldObject.GetPrivateState<StructurePrivateState>());
                    }
                }
            }
        }

        public static void ServerOnRaid(Vector2Ushort tilePosition, double raidRadius, ICharacter byCharacter)
        {
            var time = Server.Game.FrameTime;
            var radius = (int)Math.Ceiling(raidRadius);
            var bounds = new RectangleInt(x: tilePosition.X - radius,
                                          y: tilePosition.Y - radius,
                                          width: 2 * radius,
                                          height: 2 * radius);

            using (var tempList = Api.Shared.GetTempList<ILogicObject>())
            {
                SharedGetAreasInBounds(bounds, tempList);

                foreach (var area in tempList)
                {
                    if (byCharacter != null
                        && ServerIsOwnedArea(area, byCharacter))
                    {
                        // don't start the raid timer if attack is performed by the owner of the area
                        continue;
                    }

                    var areaPrivateState = area.GetPrivateState<LandClaimAreaPrivateState>();
                    areaPrivateState.LastRaidTime = time;

                    Logger.Important(
                        string.Format("Land claim area being raided: {0}{1}Land owners: {2}",
                                      areaPrivateState.ServerLandClaimWorldObject,
                                      Environment.NewLine,
                                      areaPrivateState.LandOwners.GetJoinedString()));
                }
            }
        }

        public static void ServerRegisterArea(ILogicObject area)
        {
            Api.Assert(area.ProtoLogicObject is LandClaimArea, "Wrong object type");
            sharedLandClaimAreas.Add(area);
        }

        public static void ServerUnregisterArea(ILogicObject area)
        {
            Api.Assert(area.ProtoLogicObject is LandClaimArea, "Wrong object type");
            sharedLandClaimAreas.Remove(area);
        }

        public static IStaticWorldObject ServerUpgrade(
            IStaticWorldObject oldStructure,
            IProtoObjectStructure upgradeStructure,
            ICharacter character)
        {
            if (!(oldStructure?.ProtoStaticWorldObject
                      is IProtoObjectLandClaim))
            {
                throw new Exception("Not a land claim structure: " + oldStructure);
            }

            var tilePosition = oldStructure.TilePosition;
            var area = ServerGetLandClaimArea(oldStructure);

            // release area
            oldStructure.GetPublicState<ObjectLandClaimPublicState>().LandClaimAreaObject = null;

            // destroy old structure
            ServerWorld.DestroyObject(oldStructure);

            // create new structure
            var upgradedObject = ServerWorld.CreateStaticWorldObject(upgradeStructure, tilePosition);

            // get area for the old land claim structure
            var areaPrivateState = LandClaimArea.GetPrivateState(area);
            var areaPublicState = LandClaimArea.GetPublicState(area);

            // update it to use upgraded land claim structure
            areaPrivateState.ServerLandClaimWorldObject = upgradedObject;
            areaPublicState.SetupAreaProperties(areaPrivateState);

            // set this area to the structure public state
            upgradedObject.GetPublicState<ObjectLandClaimPublicState>()
                          .LandClaimAreaObject = area;

            Logger.Important($"Successfully upgraded: {oldStructure} to {upgradedObject}", character);

            Instance.CallClient(
                Server.Characters.EnumerateAllPlayerCharacters(onlyOnline: true),
                _ => _.ClientRemote_OnLandClaimUpgraded(area));

            return upgradedObject;
        }

        public static RectangleInt SharedCalculateLandClaimAreaBounds(Vector2Ushort centerTilePosition, ushort size)
        {
            var worldBounds = IsServer
                                  ? ServerWorld.WorldBounds
                                  : ClientWorld.WorldBounds;
            var pos = centerTilePosition;
            var halfSize = size / 2.0;

            var start = new Vector2Ushort(
                (ushort)Math.Max(Math.Ceiling(pos.X - halfSize), worldBounds.MinX),
                (ushort)Math.Max(Math.Ceiling(pos.Y - halfSize), worldBounds.MinY));

            var endX = Math.Min(Math.Ceiling(pos.X + halfSize), worldBounds.MaxX);
            var endY = Math.Min(Math.Ceiling(pos.Y + halfSize), worldBounds.MaxY);

            var calculatedSize = new Vector2Ushort((ushort)(endX - start.X),
                                                   (ushort)(endY - start.Y));
            return new RectangleInt(start, size: calculatedSize);
        }

        public static Vector2Ushort SharedCalculateLandClaimObjectCenterTilePosition(
            Vector2Ushort tilePosition,
            IProtoObjectLandClaim protoObjectLandClaim)
        {
            return tilePosition.AddAndClamp(protoObjectLandClaim.Layout.Center.ToVector2Int());
        }

        public static Vector2Ushort SharedCalculateLandClaimObjectCenterTilePosition(IStaticWorldObject landClaimObject)
        {
            return SharedCalculateLandClaimObjectCenterTilePosition(
                landClaimObject.TilePosition,
                (IProtoObjectLandClaim)landClaimObject.ProtoStaticWorldObject);
        }

        public static bool SharedCheckCanDeconstruct(IStaticWorldObject worldObject, ICharacter character)
        {
            // Please note: the game already have validated that the target object is a structure
            if (worldObject.ProtoGameObject is ObjectWallDestroyed)
            {
                // always allow deconstruct a destroyed wall object even if it's in another player's land claim
                return true;
            }

            if (CreativeModeSystem.SharedIsInCreativeMode(character))
            {
                // operator can deconstruct any structure
                return true;
            }

            RectangleInt worldObjectBounds;
            {
                var temp = worldObject.ProtoStaticWorldObject.Layout.Bounds;
                var tilePosition = worldObject.TilePosition;
                worldObjectBounds = new RectangleInt(
                    temp.MinX + tilePosition.X,
                    temp.MinY + tilePosition.Y,
                    temp.Size.X,
                    temp.Size.Y);
            }

            var isThereAnyArea = false;
            foreach (var area in sharedLandClaimAreas)
            {
                var areaBounds = SharedGetLandClaimAreaBounds(area);
                if (!areaBounds.IntersectsLoose(worldObjectBounds))
                {
                    continue;
                }

                isThereAnyArea = true;
                // intersection with area found - check if player owns the area
                if (SharedIsOwnedArea(area, character))
                {
                    // player own the area
                    return true;
                }
            }

            // no area or not owned area
            if (!isThereAnyArea)
            {
                // no area
                if (worldObject.ProtoGameObject is ProtoObjectConstructionSite)
                {
                    // can deconstruct blueprints if there is no land claim area
                    return true;
                }
            }

            return false;
        }

        // need to verify that area bounds will not intersect with the any existing areas (except from the same founder)
        public static bool SharedCheckCanPlaceOrUpgradeLandClaimThere(
            IProtoObjectLandClaim protoObjectLandClaim,
            Vector2Ushort centerTilePosition,
            ICharacter forCharacter)
        {
            var newAreaBounds = SharedCalculateLandClaimAreaBounds(
                centerTilePosition,
                protoObjectLandClaim.LandClaimSize);

            foreach (var area in sharedLandClaimAreas)
            {
                var areaBoundsDirect = SharedGetLandClaimAreaBounds(area);
                var areaBoundsWithPadding = areaBoundsDirect.Inflate(LandClaimAreaGracePaddingSize,
                                                                     LandClaimAreaGracePaddingSize);

                if (!areaBoundsWithPadding.IntersectsLoose(newAreaBounds))
                {
                    // there is no area (even with the padding/grace area)
                    continue;
                }

                if (!SharedIsOwnedArea(area, forCharacter))
                {
                    // the grace/padding area of another player's land claim owner
                    return false;
                }
            }

            return true;
        }

        public static ILogicObject SharedGetAreaAtPosition(Vector2Ushort tilePosition)
        {
            // TODO: the lookup is really slow on the populated servers. Consider Grid or QuadTree optimization to locate areas quickly.
            foreach (var area in sharedLandClaimAreas)
            {
                var areaBounds = SharedGetLandClaimAreaBounds(area);
                if (!areaBounds.Contains(tilePosition))
                {
                    continue;
                }

                return area;
            }

            return null;
        }

        /// <summary>
        /// Gets only a single found area in bounds.
        /// </summary>
        public static ILogicObject SharedGetAreaInBounds(RectangleInt bounds)
        {
            // TODO: the lookup is really slow on the populated servers. Consider Grid or QuadTree optimization to locate areas quickly.
            foreach (var area in sharedLandClaimAreas)
            {
                var areaBounds = SharedGetLandClaimAreaBounds(area);
                if (areaBounds.IntersectsLoose(bounds))
                {
                    return area;
                }
            }

            return null;
        }

        public static void SharedGetAreasInBounds(RectangleInt bounds, ITempList<ILogicObject> result)
        {
            if (result.Count > 0)
            {
                result.Clear();
            }

            // TODO: the lookup is really slow on the populated servers. Consider Grid or QuadTree optimization to locate areas quickly.
            foreach (var area in sharedLandClaimAreas)
            {
                var areaBounds = SharedGetLandClaimAreaBounds(area);
                if (areaBounds.IntersectsLoose(bounds))
                {
                    result.Add(area);
                }
            }
        }

        public static RectangleInt SharedGetLandClaimAreaBounds(ILogicObject area)
        {
            var publicState = LandClaimArea.GetPublicState(area);
            return SharedCalculateLandClaimAreaBounds(
                publicState.LandClaimCenterTilePosition,
                publicState.LandClaimSize);
        }

        public static bool SharedIsFoundedArea(ILogicObject area, ICharacter forCharacter)
        {
            if (area == null)
            {
                Logger.Warning(nameof(ServerIsOwnedArea) + " - argument area is null");
                return false;
            }

            if (IsClient && !area.ClientHasPrivateState)
            {
                // not an owner so not a founder for sure
                return false;
            }

            var privateState = LandClaimArea.GetPrivateState(area);
            return privateState.LandClaimFounder == forCharacter.Name;
        }

        public static bool SharedIsOwnedArea(ILogicObject area, ICharacter forCharacter)
        {
            return IsServer
                       ? ServerIsOwnedArea(area, forCharacter)
                       : ClientIsOwnedArea(area);
        }

        private static void ServerOnAddLandOwner(ILogicObject area, ICharacter playerToAdd, bool notify)
        {
            // add this with some delay to prevent from the bug when the player name listed twice due to the late delta-replication
            if (notify)
            {
                ServerTimersSystem.AddAction(
                    delaySeconds: 0.1,
                    OnAddLandOwner);
            }
            else
            {
                OnAddLandOwner();
            }

            void OnAddLandOwner()
            {
                if (!ServerIsOwnedArea(area, playerToAdd))
                {
                    // The owner is already removed during the short delay? That's was quick!
                    return;
                }

                ServerWorld.EnterPrivateScope(playerToAdd, area);
                if (notify)
                {
                    Instance.CallClient(
                        playerToAdd,
                        _ => _.ClientRemote_OnLandOwnerStateChanged(area, true));
                }
            }
        }

        private static void ServerOnRemoveLandOwner(ILogicObject area, ICharacter removedPlayer)
        {
            InteractableStaticWorldObjectHelper.ServerTryAbortInteraction(
                removedPlayer,
                LandClaimArea.GetPrivateState(area).ServerLandClaimWorldObject);

            ServerWorld.ExitPrivateScope(removedPlayer, area);

            Instance.CallClient(removedPlayer, _ => _.ClientRemote_OnLandOwnerStateChanged(area, false));
        }

        private static bool SharedIsOwnedAreaUnderRaid(ILogicObject area)
        {
            var areaPrivateState = area.GetPrivateState<LandClaimAreaPrivateState>();
            if (!areaPrivateState.LastRaidTime.HasValue)
            {
                return false;
            }

            var time = Api.IsClient
                           ? Client.CurrentGame.ServerFrameTimeRounded
                           : Server.Game.FrameTime;

            var timeSinceRaid = time - areaPrivateState.LastRaidTime.Value;
            return timeSinceRaid < RaidCooldownDurationSeconds;
        }

        private void ClientRemote_OnCannotInteractNotOwner(IStaticWorldObject worldObject)
        {
            worldObject.ProtoStaticWorldObject.ClientOnCannotInteract(
                worldObject,
                // TODO: move this text to here
                DeconstructionSystem.NotificationNotLandOwner_Message,
                isOutOfRange: false);
        }

        private void ClientRemote_OnLandClaimUpgraded(ILogicObject area)
        {
            ClientLandClaimAreaManager.OnLandClaimUpgraded(area);
        }

        private void ClientRemote_OnLandOwnerStateChanged(ILogicObject area, bool isOwned)
        {
            Logger.Important("Land claim area ownership update: " + area + " isOwned=" + isOwned);
            ClientLandClaimAreaManager.OnLandOwnerStateChanged(area, isOwned);
        }

        private ILogicObject ServerRemote_AcquireLandClaimManager()
        {
            var character = ServerRemoteContext.Character;
            Logger.Important("Land claim areas requested from server");
            ServerWorld.ForceEnterScope(character, serverLandClaimManagerInstance);

            // add to the character private scope all owned areas
            foreach (var area in sharedLandClaimAreas)
            {
                if (ServerIsOwnedArea(area, character))
                {
                    ServerWorld.EnterPrivateScope(character, area);
                }
            }

            return serverLandClaimManagerInstance;
        }

        private string ServerRemote_SetAreaOwners(ILogicObject area, List<string> newOwners)
        {
            var owner = ServerRemoteContext.Character;
            var privateState = LandClaimArea.GetPrivateState(area);
            var currentOwners = privateState.LandOwners;

            if (!currentOwners.Contains(owner.Name))
            {
                return WorldObjectOwnersSystem.DialogCannotSetOwners_MessageNotOwner;
            }

            if (!((IProtoObjectWithOwnersList)privateState.ServerLandClaimWorldObject.ProtoStaticWorldObject)
                    .SharedCanEditOwners(privateState.ServerLandClaimWorldObject, owner))
            {
                return WorldObjectOwnersSystem.DialogCannotSetOwners_MessageCannotEdit;
            }

            currentOwners.GetDiff(newOwners, out var ownersToAdd, out var ownersToRemove);
            if (currentOwners.Count - ownersToRemove.Count <= 0)
            {
                return WorldObjectOwnersSystem.DialogCannotSetOwners_MessageCannotRemoveLastOwner;
            }

            if (ownersToRemove.Contains(owner.Name))
            {
                return WorldObjectOwnersSystem.DialogCannotSetOwners_MessageCannotRemoveSelf;
            }

            foreach (var n in ownersToAdd)
            {
                var name = n;
                var playerToAdd = Server.Characters.GetPlayerCharacter(name);
                if (playerToAdd == null)
                {
                    return string.Format(WorldObjectOwnersSystem.DialogCannotSetOwners_MessageFormatPlayerNotFound,
                                         name);
                }

                // get proper player name
                name = playerToAdd.Name;
                if (currentOwners.AddIfNotContains(name))
                {
                    Logger.Important($"Added land owner: {name}, land: {area}", characterRelated: owner);
                    ServerOnAddLandOwner(area, playerToAdd, notify: true);
                }
            }

            foreach (var name in ownersToRemove)
            {
                if (!currentOwners.Remove(name))
                {
                    continue;
                }

                Logger.Important($"Removed land owner: {name}, land: {area}", characterRelated: owner);

                var removedPlayer = Server.Characters.GetPlayerCharacter(name);
                if (removedPlayer == null)
                {
                    continue;
                }

                ServerOnRemoveLandOwner(area, removedPlayer);
            }

            return null;
        }

        [PrepareOrder(afterType: typeof(BootstrapperServerCore))]
        public class BootstrapperLandClaimSystem : BaseBootstrapper
        {
            public override void ClientInitialize()
            {
                Client.Characters.CurrentPlayerCharacterChanged += ClientTryRequestAreasAsync;

                ClientTryRequestAreasAsync();

                async void ClientTryRequestAreasAsync()
                {
                    if (Api.Client.Characters.CurrentPlayerCharacter == null)
                    {
                        return;
                    }

                    Logger.Important("Land claim areas requested from server");

                    var landClaimManagerInstance = await Instance.CallServer(
                                                       _ => _.ServerRemote_AcquireLandClaimManager());
                    var areas = LandClaimAreaManager.GetPublicState(landClaimManagerInstance)
                                                    .LandClaimAreas;
                    Logger.Important($"Land claim areas received from server: {areas.Count} areas total");

                    ClientLandClaimAreaManager.SetAreas(areas);
                    sharedLandClaimAreas = areas;
                }
            }

            public override void ServerInitialize(IServerConfiguration serverConfiguration)
            {
                Server.World.WorldBoundsChanged += this.ServerWorldBoundsChangedHandler;
                ServerLoadSystem();
            }

            private static void ServerLoadSystem()
            {
                const string key = nameof(LandClaimAreaManager);
                if (Server.Database.TryGet(key, key, out ILogicObject savedManager))
                {
                    Server.World.DestroyObject(savedManager);
                }

                serverLandClaimManagerInstance = Server.World.CreateLogicObject<LandClaimAreaManager>();
                var publicState = LandClaimAreaManager.GetPublicState(serverLandClaimManagerInstance);
                publicState.LandClaimAreas = new NetworkSyncList<ILogicObject>();

                Server.Database.Set(key, key, serverLandClaimManagerInstance);

                sharedLandClaimAreas = LandClaimAreaManager.GetPublicState(serverLandClaimManagerInstance)
                                                           .LandClaimAreas;

                foreach (var area in sharedLandClaimAreas)
                {
                    var areaPrivateState = LandClaimArea.GetPrivateState(area);
                    var areaPublicState = LandClaimArea.GetPublicState(area);
                    areaPublicState.SetupAreaProperties(areaPrivateState);
                }
            }

            private void ServerWorldBoundsChangedHandler()
            {
                const string key = nameof(LandClaimAreaManager);
                Server.Database.Remove(key, key);
                Server.World.DestroyObject(serverLandClaimManagerInstance);
                ServerLoadSystem();
            }
        }
    }
}