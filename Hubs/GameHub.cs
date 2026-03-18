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

        public async Task JoinGame(string gameId, string playerName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

            var room = Rooms.GetOrAdd(gameId, _ => new RoomState());
            string assignedColor;

            lock (room)
            {
                if (room.RedConnectionId == null)
                {
                    room.RedConnectionId = Context.ConnectionId;
                    room.Game.RedPlayerName = playerName;
                    assignedColor = "r";
                }
                else if (room.BlackConnectionId == null)
                {
                    room.BlackConnectionId = Context.ConnectionId;
                    room.Game.BlackPlayerName = playerName;
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
                room.Game.TurnStartedAtUtc = DateTime.UtcNow;
                await Clients.Group(gameId).SendAsync("GameStarted");
            }

            await BroadcastOpenRooms();
        }

        public async Task MakeMove(string gameId, Move move)
        {
            if (!Rooms.TryGetValue(gameId, out var room))
                return;

            bool moveAccepted = false;

            lock (room)
            {
                _gameService.ApplyTurnClock(room.Game);

                if (!room.Game.GameOver && _gameService.IsValidMove(room.Game, move))
                {
                    _gameService.ApplyMove(room.Game, move);
                    _gameService.UpdateGameStatus(room.Game);
                    moveAccepted = true;
                }
            }

            if (moveAccepted || room.Game.GameOver)
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
                var redName = room.Game.RedPlayerName;
                var blackName = room.Game.BlackPlayerName;

                room.Game = new GameState
                {
                    RedPlayerName = redName,
                    BlackPlayerName = blackName,
                    TurnStartedAtUtc = DateTime.UtcNow
                };
            }

            await Clients.Group(gameId).SendAsync("ReceiveState", room.Game);
        }

        public async Task SendChatMessage(string gameId, string sender, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var safeMessage = message.Trim();
            if (safeMessage.Length > 200)
                safeMessage = safeMessage[..200];

            await Clients.Group(gameId).SendAsync("ReceiveChatMessage", sender, safeMessage);
        }

        public async Task RequestOpenRooms()
        {
            await Clients.Caller.SendAsync("ReceiveOpenRooms", GetOpenRooms());
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
                        room.Game.RedPlayerName = "";
                        changed = true;
                    }

                    if (room.BlackConnectionId == Context.ConnectionId)
                    {
                        room.BlackConnectionId = null;
                        room.Game.BlackPlayerName = "";
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

            await BroadcastOpenRooms();
            await base.OnDisconnectedAsync(exception);
        }

        private async Task BroadcastOpenRooms()
        {
            await Clients.All.SendAsync("ReceiveOpenRooms", GetOpenRooms());
        }

        private static List<OpenRoomInfo> GetOpenRooms()
        {
            return Rooms
                .Where(r => (r.Value.RedConnectionId == null) ^ (r.Value.BlackConnectionId == null))
                .Select(r =>
                {
                    bool redWaiting = r.Value.RedConnectionId != null && r.Value.BlackConnectionId == null;

                    return new OpenRoomInfo
                    {
                        RoomId = r.Key,
                        WaitingPlayerName = redWaiting ? r.Value.Game.RedPlayerName : r.Value.Game.BlackPlayerName,
                        WaitingColor = redWaiting ? "Kırmızı" : "Siyah"
                    };
                })
                .OrderBy(r => r.RoomId)
                .ToList();
        }

        private class RoomState
        {
            public string? RedConnectionId { get; set; }
            public string? BlackConnectionId { get; set; }
            public GameState Game { get; set; } = new GameState();
        }

        private class OpenRoomInfo
        {
            public string RoomId { get; set; } = "";
            public string WaitingPlayerName { get; set; } = "";
            public string WaitingColor { get; set; } = "";
        }
    }
}