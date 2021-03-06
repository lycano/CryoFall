﻿namespace AtomicTorch.CBND.CoreMod.Systems.Chat
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AtomicTorch.CBND.CoreMod.Helpers.Client;
    using AtomicTorch.CBND.CoreMod.Systems.OnlinePlayers;
    using AtomicTorch.CBND.CoreMod.Systems.Party;
    using AtomicTorch.CBND.CoreMod.Systems.ServerPlayerAccess;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Logic;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using JetBrains.Annotations;

    public class ChatSystem : ProtoSystem<ChatSystem>
    {
        public const int MaxChatEntryLength = 225;

        public const string NotificationCurrentPlayerMuted =
            "You are banned from participating in chat by server administrator. Your ban expires in: {0}";

        public const string PlayerWentOfflineChatMessageFormat = "{0} left the game";

        public const string PlayerWentOnlineChatMessageFormat = "{0} joined the game";

        // dict source character -> (dict other character -> chat room holder)
        private static IDictionary<string, Dictionary<string, ILogicObject>> serverPrivateChatRooms;

        private static ILogicObject sharedGlobalChatRoomHolder;

        private static ILogicObject sharedLocalChatRoomHolder;

        public delegate void ClientReceivedMessageDelegate(BaseChatRoom chatRoom, in ChatEntry chatEntry);

        public static event Action<BaseChatRoom> ClientChatRoomAdded;

        public static event ClientReceivedMessageDelegate ClientChatRoomMessageReceived;

        public static event Action<BaseChatRoom> ClientChatRoomRemoved;

        public override string Name => "Chat system";

        public static void ClientOnChatRoomAdded(ILogicObject chatRoomHolder)
        {
            var chatRoom = SharedGetChatRoom(chatRoomHolder);
            switch (chatRoom)
            {
                case ChatRoomGlobal _:
                    sharedGlobalChatRoomHolder = chatRoomHolder;
                    break;

                case ChatRoomLocal _:
                    sharedLocalChatRoomHolder = chatRoomHolder;
                    break;
            }

            Logger.Info($"{nameof(ChatSystem)}: added a chat room - {chatRoom}");
            Api.SafeInvoke(() => ClientChatRoomAdded?.Invoke(chatRoom));
        }

        public static void ClientOnChatRoomRemoved(ILogicObject chatRoomHolder)
        {
            var chatRoom = SharedGetChatRoom(chatRoomHolder);
            Logger.Info($"{nameof(ChatSystem)}: removed a chat room - {chatRoom}");
            Api.SafeInvoke(() => ClientChatRoomRemoved?.Invoke(chatRoom));
        }

        public static Task<ILogicObject> ClientOpenPrivateChat(string withCharacterName)
        {
            var allChats = Client.World.FindGameObjectsOfProto<ILogicObject, ChatRoomHolder>();
            foreach (var chatRoomHolder in allChats)
            {
                if (SharedGetChatRoom(chatRoomHolder) is ChatRoomPrivate privateChatRoom
                    && (privateChatRoom.CharacterA == withCharacterName
                     || privateChatRoom.CharacterB == withCharacterName))
                {
                    // found a local chat instance with this character
                    return Task.FromResult(chatRoomHolder);
                }
            }

            return Instance.CallServer(_ => _.ServerRemote_CreatePrivateChatRoom(withCharacterName));
        }

        public static void ClientSendMessageToRoom(
            [NotNull] BaseChatRoom chatRoom,
            string message)
        {
            chatRoom.ClientOnMessageReceived(new ChatEntry(
                                                 ClientCurrentCharacterHelper.Character.Name,
                                                 message,
                                                 isService: false,
                                                 DateTime.Now));

            var chatRoomHolder = (ILogicObject)chatRoom.GameObject;
            Instance.CallServer(_ => _.ServerRemote_ClientSendMessage(chatRoomHolder, message));
        }

        public static void ServerAddChatRoomToPlayerScope(ICharacter character, ILogicObject chatRoomHolder)
        {
            if (character.IsOnline)
            {
                Server.World.EnterPrivateScope(character, chatRoomHolder);
                Server.World.ForceEnterScope(character, chatRoomHolder);
            }
        }

        public static ILogicObject ServerCreateChatRoom(BaseChatRoom chatRoom)
        {
            var chatRoomHolder = Server.World.CreateLogicObject<ChatRoomHolder>();
            ChatRoomHolder.ServerSetup(chatRoomHolder, chatRoom);
            return chatRoomHolder;
        }

        public static void ServerRemoveChatRoomFromPlayerScope(ICharacter character, ILogicObject chatRoomHolder)
        {
            if (character.IsOnline)
            {
                Server.World.ForceExitScope(character, chatRoomHolder);
            }
        }

        public static void ServerSendGlobalServiceMessage(string message)
        {
            ServerSendServiceMessage(sharedGlobalChatRoomHolder, message);
        }

        public static void ServerSendServiceMessage(ILogicObject chatRoomHolder, string message)
        {
            var charactersDestination = SharedGetChatRoom(chatRoomHolder)
                .ServerEnumerateMessageRecepients(forPlayer: null);

            var chatEntry = new ChatEntry(from: null,
                                          message,
                                          isService: true,
                                          DateTime.Now);

            Instance.ServerSendMessage(chatRoomHolderObject: chatRoomHolder,
                                       chatEntry,
                                       charactersDestination);
        }

        public static BaseChatRoom SharedGetChatRoom(ILogicObject chatRoomHolder)
        {
            return chatRoomHolder.GetPrivateState<ChatRoomHolder.ChatRoomPrivateState>().ChatRoom;
        }

        public ILogicObject ServerRemote_CreatePrivateChatRoom(string inviteeName)
        {
            var currentCharacter = ServerRemoteContext.Character;
            var currentCharacterName = currentCharacter.Name;
            if (!serverPrivateChatRooms.TryGetValue(currentCharacterName,
                                                    out var currentCharacterPrivateChatRooms))
            {
                currentCharacterPrivateChatRooms = new Dictionary<string, ILogicObject>();
                serverPrivateChatRooms[currentCharacterName] = currentCharacterPrivateChatRooms;
            }

            if (currentCharacterPrivateChatRooms.TryGetValue(inviteeName, out var chatRoomHolder))
            {
                Logger.Warning(
                    $"Private chat room already exist between players {currentCharacter} and {inviteeName}",
                    currentCharacter);
                return chatRoomHolder;
            }

            var inviteeCharacter = Server.Characters.GetPlayerCharacter(inviteeName);
            if (inviteeCharacter == null)
            {
                throw new Exception($"Private chat room cannot be created - character not found {inviteeName}");
            }

            // create chat room
            chatRoomHolder = ServerCreateChatRoom(new ChatRoomPrivate(currentCharacterName, inviteeName));

            // register chat room for current character
            currentCharacterPrivateChatRooms.Add(inviteeName, chatRoomHolder);

            // register chat room for other character
            if (!serverPrivateChatRooms.TryGetValue(inviteeName,
                                                    out var inviteePrivateChatRooms))
            {
                inviteePrivateChatRooms = new Dictionary<string, ILogicObject>();
                serverPrivateChatRooms[inviteeName] = inviteePrivateChatRooms;
            }

            inviteePrivateChatRooms[currentCharacterName] = chatRoomHolder;

            ServerAddChatRoomToPlayerScope(currentCharacter, chatRoomHolder);
            if (currentCharacter != inviteeCharacter)
            {
                ServerAddChatRoomToPlayerScope(inviteeCharacter, chatRoomHolder);
            }

            Logger.Important(
                $"Private chat room created between players {currentCharacter} and {inviteeName}",
                currentCharacter);
            return chatRoomHolder;
        }

        protected override void PrepareSystem()
        {
            if (IsClient)
            {
                ClientChatBlockList.Initialize();
                OnlinePlayersSystem.ClientOnPlayerAddedOrRemoved +=
                    this.ClientOnlinePlayersSystemOnPlayerAddedOrRemovedHandler;
                return;
            }

            Server.Characters.PlayerOnlineStateChanged += ServerPlayerOnlineStateChangedHandler;
        }

        private static void ClientReceiveChatEntry(ILogicObject chatRoomHolderObject, ChatEntry chatEntry)
        {
            if (ClientChatBlockList.IsBlocked(chatEntry.From))
            {
                Logger.Info("Received a message from the blocked player: " + chatEntry.From);
                return;
            }

            var chatRoom = SharedGetChatRoom(chatRoomHolderObject);
            chatRoom.ClientOnMessageReceived(chatEntry);

            ClientChatRoomMessageReceived?.Invoke(chatRoom, chatEntry);
        }

        private static void ServerLogNewChatEntry(uint chatRoomId, string message, string fromPlayerName)
        {
            // ReSharper disable once CanExtractXamlLocalizableStringCSharp
            var text = $"ChatId={chatRoomId} From=\"{fromPlayerName}\":{Environment.NewLine}{message}";
            Logger.Important(text);
            Server.Core.AddChatLogEntry(text);
        }

        private static void ServerPlayerOnlineStateChangedHandler(ICharacter playerCharacter, bool isOnline)
        {
            // just write entry into the server log file
            // no need to send it to client (clients are using OnlinePlayersSystem to display these messages)
            var name = playerCharacter.Name;
            var message = string.Format(isOnline
                                            ? PlayerWentOnlineChatMessageFormat
                                            : PlayerWentOfflineChatMessageFormat,
                                        name);

            Server.Core.AddChatLogEntry(message);
        }

        private void ClientOnlinePlayersSystemOnPlayerAddedOrRemovedHandler(
            string name,
            bool isOnline)
        {
            if (name == Client.Characters.CurrentPlayerCharacter.Name
                || sharedGlobalChatRoomHolder == null)
            {
                return;
            }

            var message = string.Format(isOnline
                                            ? PlayerWentOnlineChatMessageFormat
                                            : PlayerWentOfflineChatMessageFormat,
                                        name);

            var chatEntry = new ChatEntry(name,
                                          message,
                                          isService: true,
                                          DateTime.Now);
            ClientReceiveChatEntry(
                chatRoomHolderObject: sharedGlobalChatRoomHolder,
                chatEntry);

            var allChats = Client.World.FindGameObjectsOfProto<ILogicObject, ChatRoomHolder>();
            foreach (var chatRoomHolder in allChats)
            {
                switch (SharedGetChatRoom(chatRoomHolder))
                {
                    case ChatRoomPrivate privateChatRoom:
                        if (privateChatRoom.CharacterA == name
                            || privateChatRoom.CharacterB == name)
                        {
                            // found a local private chat instance with this character
                            ClientReceiveChatEntry(
                                chatRoomHolderObject: (ILogicObject)privateChatRoom.GameObject,
                                chatEntry);
                        }

                        break;

                    case ChatRoomParty partyChatRoom:
                        if (PartySystem.ClientIsPartyMember(name))
                        {
                            ClientReceiveChatEntry(
                                chatRoomHolderObject: (ILogicObject)partyChatRoom.GameObject,
                                chatEntry);
                        }

                        break;
                }
            }
        }

        private void ClientRemote_OnMuted(ILogicObject chatRoomHolder, double secondsRemains)
        {
            ClientReceiveChatEntry(
                chatRoomHolder,
                new ChatEntry(from: null,
                              message: string.Format(NotificationCurrentPlayerMuted,
                                                     ClientTimeFormatHelper.FormatTimeDuration(
                                                         secondsRemains)),
                              isService: true,
                              DateTime.Now));
        }

        [RemoteCallSettings(DeliveryMode.ReliableOrdered)]
        private void ClientRemote_ReceiveMessage(
            [NotNull] ILogicObject chatRoomHolderObject,
            ChatEntry chatEntry)
        {
            ClientReceiveChatEntry(chatRoomHolderObject, chatEntry);
        }

        [RemoteCallSettings(DeliveryMode.ReliableOrdered)]
        private void ServerRemote_ClientSendMessage(
            ILogicObject chatRoomHolder,
            string message)
        {
            Api.Assert(chatRoomHolder != null, "Chat room not found");

            var character = ServerRemoteContext.Character;
            var characterName = character.Name;

            if (ServerPlayerMuteSystem.IsMuted(characterName, out var secondsRemains))
            {
                this.CallClient(character,
                                _ => _.ClientRemote_OnMuted(chatRoomHolder, secondsRemains));
                return;
            }

            if (!Server.Core.IsInPrivateScope(character, chatRoomHolder))
            {
                Logger.Error("Player doesn't have access to chat room (not in the private scope): "
                             + chatRoomHolder
                             + " - cannot accept an incoming message here");
                return;
            }

            if (message.Length > MaxChatEntryLength)
            {
                message = message.Substring(0, MaxChatEntryLength);
            }

            var chatRoom = SharedGetChatRoom(chatRoomHolder);

            var chatEntry = new ChatEntry(characterName,
                                          message,
                                          isService: false,
                                          DateTime.Now);

            chatRoom.ServerAddMessageToLog(chatEntry);

            using (var tempDestination = Api.Shared.WrapInTempList(
                chatRoom.ServerEnumerateMessageRecepients(character)))
            {
                tempDestination.Remove(character);
                this.ServerSendMessage(chatRoomHolder, chatEntry, tempDestination);
            }

            ServerLogNewChatEntry(chatRoomHolder.Id, message, characterName);
        }

        private void ServerRemote_RequestChatRooms()
        {
            var character = ServerRemoteContext.Character;

            ServerAddChatRoomToPlayerScope(character, sharedGlobalChatRoomHolder);
            ServerAddChatRoomToPlayerScope(character, sharedLocalChatRoomHolder);

            if (serverPrivateChatRooms.TryGetValue(character.Name, out var characterPrivateChatRooms))
            {
                foreach (var chatRoomHolder in characterPrivateChatRooms.Values)
                {
                    ServerAddChatRoomToPlayerScope(character, chatRoomHolder);
                }
            }
        }

        private void ServerSendMessage(
            ILogicObject chatRoomHolderObject,
            ChatEntry chatEntry,
            IEnumerable<ICharacter> charactersDestination)
        {
            this.CallClient(charactersDestination,
                            _ => _.ClientRemote_ReceiveMessage(chatRoomHolderObject, chatEntry));
        }

        private class Bootstrapper : BaseBootstrapper
        {
            public override void ClientInitialize()
            {
                Client.Characters.CurrentPlayerCharacterChanged += Refresh;

                Refresh();

                void Refresh()
                {
                    sharedLocalChatRoomHolder = null;
                    sharedGlobalChatRoomHolder = null;

                    if (Api.Client.Characters.CurrentPlayerCharacter != null)
                    {
                        Instance.CallServer(_ => _.ServerRemote_RequestChatRooms());
                    }
                }
            }

            public override void ServerInitialize(IServerConfiguration serverConfiguration)
            {
                var database = Server.Database;
                if (!database.TryGet(nameof(ChatSystem),
                                     "GlobalChatRoomHolder",
                                     out sharedGlobalChatRoomHolder))
                {
                    sharedGlobalChatRoomHolder = ServerCreateChatRoom(new ChatRoomGlobal());
                    database.Set(nameof(ChatSystem), "GlobalChatRoomHolder", sharedGlobalChatRoomHolder);
                }

                if (!database.TryGet(nameof(ChatSystem),
                                     "LocalChatRoomHolder",
                                     out sharedLocalChatRoomHolder))
                {
                    sharedLocalChatRoomHolder = ServerCreateChatRoom(new ChatRoomLocal());
                    database.Set(nameof(ChatSystem), "LocalChatRoomHolder", sharedLocalChatRoomHolder);
                }

                if (!database.TryGet(nameof(ChatSystem),
                                     nameof(serverPrivateChatRooms),
                                     out serverPrivateChatRooms))
                {
                    serverPrivateChatRooms = new Dictionary<string, Dictionary<string, ILogicObject>>();
                    database.Set(nameof(ChatSystem),
                                 nameof(serverPrivateChatRooms),
                                 serverPrivateChatRooms);
                }
            }
        }
    }
}