namespace Slither;

using Microsoft.AspNetCore.SignalR;

public class GameService(IHubContext<GameHub> hub) : IHostedService
{
  private const float WorldSize      = 3000f;
  private const int   MaxFood        = 400;
  private const int   SegSpacing     = 1;
  private const float TurnSpeed      = 0.09f;
  private const int   TickRateMs     = 33;   // ~30 TPS
  private const int   BroadcastEvery = 1;    // broadcast every N ticks

  private readonly Dictionary<string, Snake> _snakes = new();
  private readonly Dictionary<string, Food>  _foods  = new();
  private readonly object _lock = new();
  private readonly Random _rng  = new();
  private CancellationTokenSource? _cts;
  private long _tick;


  static float Lerp(float a, float b, float t)
  {
    return a + (b - a) * t;
  }

  public Task StartAsync(CancellationToken ct)
  {
    for (int i = 0; i < MaxFood; i++)
    {
      SpawnFood();
    }

    _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    _ = Task.Run(() => GameLoop(_cts.Token), _cts.Token);
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken ct) { _cts?.Cancel(); return Task.CompletedTask; }

  public void Spawn(string id, string name)
  {
    var colors = new[] {
      "#4CAF50",
      "#2196F3",
      "#FF5722",
      "#9C27B0",
      "#FF9800",
      "#00BCD4",
      "#F44336",
      "#E91E63",
      "#00E676",
      "#8BC34A"
    };

    float cx         = 300 + (float)_rng.NextDouble() * (WorldSize - 600);
    float cy         = 300 + (float)_rng.NextDouble() * (WorldSize - 600);
    float startAngle = (float)(_rng.NextDouble() * MathF.PI * 2);

    var snake = new Snake
    {
      Id    = id,
      Name  = name,
      Color = colors[_rng.Next(colors.Length)],
      Angle = startAngle,
      TargetAngle = startAngle,
    };
    for (int i = 0; i < 800; i++) snake.Path.Add(new(cx, cy));

    lock (_lock) _snakes[id] = snake;
  }

  public void Remove(string id)
  {
    lock (_lock)
    {
      if (_snakes.Remove(id, out var snake))
        DropFood(snake, full: true);
    }
  }

  public void SetAngle(string id, float angle)
  {
    lock (_lock)
    {
      if (_snakes.TryGetValue(id, out var s))
        s.TargetAngle = angle;
    }
  }

  private async Task GameLoop(CancellationToken ct)
  {
    while (!ct.IsCancellationRequested)
    {
      var sw = System.Diagnostics.Stopwatch.StartNew();

      var dead = Step();

      foreach (var id in dead)
        _ = hub.Clients.Client(id)
        .SendAsync("Died", cancellationToken: CancellationToken.None);

      if (_tick % BroadcastEvery == 0)
      {
        var snapshot = Snapshot();
        _ = hub.Clients.All
          .SendAsync("GameState", snapshot, cancellationToken: CancellationToken.None);
      }

      int delay = Math.Max(1, TickRateMs - (int)sw.ElapsedMilliseconds);
      await Task.Delay(delay, ct).ConfigureAwait(false);
    }
  }

  private List<string> Step()
  {
    _tick++;
    var dead = new List<string>();

    lock (_lock)
    {
      foreach (var snake in _snakes.Values)
      {
        float diff = NormAngle(snake.TargetAngle - snake.Angle);
        snake.Angle += MathF.Abs(diff) <= TurnSpeed
          ? diff
          : MathF.Sign(diff) * TurnSpeed;

        float nx = snake.Head.X + MathF.Cos(snake.Angle) * snake.Speed;
        float ny = snake.Head.Y + MathF.Sin(snake.Angle) * snake.Speed;

        if (nx <= 0 || nx >= WorldSize || ny <= 0 || ny >= WorldSize)
        {
          dead.Add(snake.Id);
          continue;
        }

        snake.Path.Insert(0, new(nx, ny));

        int maxPath = (Math.Min(snake.Length, 120) + 2) * SegSpacing + 20;
        if (snake.Path.Count > maxPath)
          snake.Path.RemoveRange(maxPath, snake.Path.Count - maxPath);

        float r = snake.Radius;
        foreach (var food in _foods.Values.ToList())
        {
          if (MathF.Abs(food.X - nx) > r + food.Radius) continue;
          if (MathF.Abs(food.Y - ny) > r + food.Radius) continue;
          if (new Vec2(food.X, food.Y).DistanceTo(new(nx, ny)) > r + food.Radius) continue;

          _foods.Remove(food.Id);
          snake.Score  += food.Value;
          snake.Length  = Math.Min(120, snake.Length + 2);
          SpawnFood();
        }
      }

      var list = _snakes.Values.ToList();
      foreach (var snake in list)
      {
        if (dead.Contains(snake.Id)) continue;
        var head = snake.Head;
        float r  = snake.Radius;

        foreach (var other in list)
        {
          if (other.Id == snake.Id || dead.Contains(other.Id)) continue;
          foreach (var seg in other.GetSegments(SegSpacing))
          {
            if (head.DistanceTo(seg) < r + other.Radius * 0.8f)
            {
              dead.Add(snake.Id);
              other.Score += Math.Max(5, snake.Score / 4);
              goto nextSnake;
            }
          }
        }
        nextSnake:;
      }

      foreach (var id in dead)
        if (_snakes.Remove(id, out var s))
          DropFood(s, full: false);
    }

    return dead;
  }

  private void DropFood(Snake snake, bool full)
  {
    var segs  = snake.GetSegments(SegSpacing);
    int count = full ? segs.Count : segs.Count / 2 + 5;
    foreach (var seg in segs.Take(count))
    {
      if (_foods.Count >= MaxFood * 2) break;
      var food = new Food
      {
        X      = Math.Clamp(seg.X + (float)(_rng.NextDouble() - 0.5) * 14, 5, WorldSize - 5),
        Y      = Math.Clamp(seg.Y + (float)(_rng.NextDouble() - 0.5) * 14, 5, WorldSize - 5),
        Color  = snake.Color,
        Radius = 4 + (float)_rng.NextDouble() * 3,
        Value  = 1,
      };
      _foods[food.Id] = food;
    }
  }

  private void SpawnFood()
  {
    var cols = new[] {
      "#FF5722",
      "#FF9800",
      "#FFC107",
      "#FFEB3B",
      "#CDDC39",
      "#8BC34A",
      "#69F0AE",
      "#40C4FF",
      "#EA80FC"
    };
    var food = new Food
    {
      X      = 20 + (float)_rng.NextDouble() * (WorldSize - 40),
      Y      = 20 + (float)_rng.NextDouble() * (WorldSize - 40),
      Color  = cols[_rng.Next(cols.Length)],
      Radius = 3 + (float)_rng.NextDouble() * 5,
      Value  = 1,
    };
    _foods[food.Id] = food;
  }

  private GameStateDto Snapshot()
  {
    lock (_lock)
    {
      var dto = new GameStateDto { Foods = _foods.Values.ToList() };
      foreach (var s in _snakes.Values)
        dto.Snakes[s.Id] = new SnakeDto
        {
          Id       = s.Id,
          Name     = s.Name,
          Segments = s.GetSegments(SegSpacing),
          Color    = s.Color,
          Score    = s.Score,
          Radius   = s.Radius,
        };
      return dto;
    }
  }

  private static float NormAngle(float a)
  {
    while (a >  MathF.PI) a -= 2 * MathF.PI;
    while (a < -MathF.PI) a += 2 * MathF.PI;
    return a;
  }
}
