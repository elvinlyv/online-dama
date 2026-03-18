using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using OnlineDama.Models;
using OnlineDama.Services;

namespace OnlineDama.Hubs
{
    public class GameHub : Hub
    {
        private static readonly ConcurrentDictionary<string, RoomState> Rooms = new();
        private readonly GameService _gameService;

        public GameHub()
        {
            _gameService = new GameService();
        }

        public async Task JoinGame(string gameId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

            var room = Rooms.GetOrAdd(gameId, _ => new RoomState());

            string assignedColor;

            lock (room)
            {
                if (room.RedConnectionId == null)
                {
                    room.RedConnectionId = Context.ConnectionId;
                    assignedColor = "r";
                }
                else if (room.BlackConnectionId == null)
                {
                    room.BlackConnectionId = Context.ConnectionId;
                    assignedColor = "b";
                }
                else
                {
                    assignedColor = "spectator";
                }
            }

            await Clients.Caller.SendAsync("PlayerAssigned", assignedColor);
            await Clients.Caller.SendAsync("ReceiveState", room.Game);

            if (room.RedConnectionId != null && room.BlackConnectionId != null)
            {
                await Clients.Group(gameId).SendAsync("GameStarted");
            }
        }

        public async Task MakeMove(string gameId, Move move)
        {
            if (!Rooms.TryGetValue(gameId, out var room))
                return;

            bool moveAccepted = false;

            lock (room)
            {
                if (_gameService.IsValidMove(room.Game, move))
                {
                    _gameService.ApplyMove(room.Game, move);
                    _gameService.UpdateGameStatus(room.Game);
                    moveAccepted = true;
                }
            }

            if (moveAccepted)
            {
                await Clients.Group(gameId).SendAsync("ReceiveState", room.Game);
            }
            else
            {
                await Clients.Caller.SendAsync("InvalidMove");
            }
        }

        public async Task ResetGame(string gameId)
        {
            if (!Rooms.TryGetValue(gameId, out var room))
                return;

            lock (room)
            {
                room.Game = new GameState();
            }

            await Clients.Group(gameId).SendAsync("ReceiveState", room.Game);
        }

        public async Task SendChatMessage(string gameId, string sender, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var safeMessage = message.Trim();
            if (safeMessage.Length > 200)
                safeMessage = safeMessage.Substring(0, 200);

            await Clients.Group(gameId).SendAsync("ReceiveChatMessage", sender, safeMessage);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            foreach (var kvp in Rooms)
            {
                var roomId = kvp.Key;
                var room = kvp.Value;
                bool changed = false;

                lock (room)
                {
                    if (room.RedConnectionId == Context.ConnectionId)
                    {
                        room.RedConnectionId = null;
                        changed = true;
                    }

                    if (room.BlackConnectionId == Context.ConnectionId)
                    {
                        room.BlackConnectionId = null;
                        changed = true;
                    }
                }

                if (changed)
                {
                    await Clients.Group(roomId).SendAsync("PlayerDisconnected");

                    lock (room)
                    {
                        if (room.RedConnectionId == null && room.BlackConnectionId == null)
                        {
                            Rooms.TryRemove(roomId, out _);
                        }
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        private class RoomState
        {
            public string? RedConnectionId { get; set; }
            public string? BlackConnectionId { get; set; }
            public GameState Game { get; set; } = new GameState();
        }
    }
}