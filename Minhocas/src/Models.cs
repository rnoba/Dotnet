namespace Slither;

public record Vec2(float X, float Y)
{
  public float DistanceTo(Vec2 other)
  {
    var dx = X - other.X;
    var dy = Y - other.Y;
    return MathF.Sqrt(dx * dx + dy * dy);
  }
}

public class Snake
{
  public string Id         { get; init; } = "";
  public string Name       { get; set;  } = "";
  public List<Vec2> Path   { get; }       = new();

  public float  Angle       { get; set; }
  public float  TargetAngle { get; set; }

  public float  Speed       { get; set; } = 3f;
  public int    Score       { get; set; }
  public int    Length      { get; set; } = 10;
  public string Color       { get; init; } = "#4CAF50";

  public float Radius => Math.Max(20f, 8f * MathF.Sqrt(Score / 10f));
  public Vec2  Head   => Path.Count > 0 ? Path[0] : new(0, 0);

  public List<Vec2> GetSegments(int spacing = 8)
  {
    var segs  = new List<Vec2>(Length);
    int total = Math.Min(Length, 120);
    for (int i = 0; i < total; i += 1)
    {
      int idx = i * spacing;
      segs.Add(idx < Path.Count ? Path[idx] : Path[^1]);
    }
    return segs;
  }
}

public class Food
{
  public string Id     { get; }       = Guid.NewGuid().ToString("N")[..8];
  public float  X      { get; init; }
  public float  Y      { get; init; }
  public string Color  { get; init; } = "#FF5722";
  public float  Radius { get; init; } = 5f;
  public int    Value  { get; init; } = 1;
}

public class SnakeDto
{
  public string     Id        { get; set; } = "";
  public string     Name      { get; set; } = "";
  public List<Vec2> Segments  { get; set; } = new();
  public string     Color     { get; set; } = "";
  public int        Score     { get; set; }
  public float      Radius    { get; set; }
}

public class GameStateDto
{
  public Dictionary<string, SnakeDto> Snakes { get; set; } = new();
  public List<Food>                   Foods  { get; set; } = new();
}
