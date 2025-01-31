using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

namespace AirCallServer.Hubs;

public interface ICallClient
{
    public Task ReceiveTextMessage(string sender, string message);
    public Task HandleNewRoomUser(string user);
    public Task HandleUserLeftRoom(string user);
}

public class CallHub : Hub<ICallClient>
{
    // TODO: merge following dictionaries into one
    // NOTE: maps connection ids to usernames
    private static readonly ConcurrentDictionary<string, string> Users = new();
    
    // NOTE: maps connection ids to room ids
    private static readonly ConcurrentDictionary<string, string> Rooms = new();
    
    public async Task SendTextMessage(string message, ILogger<CallHub> logger)
    {
        if (Users.TryGetValue(Context.ConnectionId, out var sender))
            await Clients.Group(Rooms[Context.ConnectionId]).ReceiveTextMessage(sender, message);
        else
        {
            logger.LogError(
                $"User {Context.ConnectionId} tried to send text message, but they haven't joined the room");

            throw new HubException("You aren't connected to any room");
        }
    }

    public async Task JoinRoom(string roomId, string userName, ILogger<CallHub> logger)
    {
        if (Users.TryAdd(Context.ConnectionId, userName))
        {
            Rooms.TryAdd(Context.ConnectionId, roomId);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            await Clients.Group(roomId).HandleNewRoomUser(userName);
            logger.LogInformation($"User {userName} ({Context.ConnectionId}) joined the room {roomId}");
        }
        else
        {
            var room = Rooms[Context.ConnectionId];
            logger.LogError($"User {userName} ({Context.ConnectionId}) tried to join the room but they have already " +
                            $"joined the room {room}");

            throw new HubException($"You have already joined the room {room}");
        }
    }

    public async Task LeaveRoom(ILogger<CallHub> logger)
    {
        if (Rooms.TryGetValue(Context.ConnectionId, out var roomId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

            Users.TryRemove(Context.ConnectionId, out var userName);
            Rooms.TryRemove(Context.ConnectionId, out _);
            Debug.Assert(userName != null);

            await Clients.Group(roomId).HandleUserLeftRoom(userName);
            logger.LogInformation($"User {userName} ({Context.ConnectionId}) left the room \"{roomId}\"");
        }
        else
        {
            logger.LogError($"User with connection id \"{Context.ConnectionId}\" tried to leave the room," +
                            $"but they never joined.");

            throw new HubException("You aren't connected to any room");
        }
    }
}