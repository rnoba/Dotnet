namespace Slither;

using Microsoft.AspNetCore.SignalR;

public class GameHub(GameService game) : Hub
{
  private static readonly Dictionary<string, string> _names = new();

  public async Task Join(string name)
  {
    name = string.IsNullOrWhiteSpace(name) ? "Anonymous" : name.Trim()[..Math.Min(name.Trim().Length, 16)];
    lock (_names) _names[Context.ConnectionId] = name;

    game.Spawn(Context.ConnectionId, name);
    await Clients.Caller.SendAsync("Init", Context.ConnectionId);
  }

  public Task UpdateAngle(float angle)
  {
    game.SetAngle(Context.ConnectionId, angle);
    return Task.CompletedTask;
  }

  public async Task Respawn()
  {
    string name;
    lock (_names) _names.TryGetValue(Context.ConnectionId, out name!);
    game.Spawn(Context.ConnectionId, name ?? "Anonymous");
    await Clients.Caller.SendAsync("Init", Context.ConnectionId);
  }

  public override Task OnDisconnectedAsync(Exception? ex)
  {
    game.Remove(Context.ConnectionId);
    lock (_names) _names.Remove(Context.ConnectionId);
    return base.OnDisconnectedAsync(ex);
  }
}
