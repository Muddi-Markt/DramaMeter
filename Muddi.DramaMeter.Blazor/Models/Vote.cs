namespace Muddi.DramaMeter.Blazor.Models;

public class Vote
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>
    /// 0 = No Drama, 1 = It's Sparking, 2 = Bottomless, 3 = Extraordinary Session
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Click angle in degrees (0°=right, 90°=top, 180°=left).
    /// Null for votes cast before the click-position feature.
    /// </summary>
    public double? ClickAngle { get; set; }

    /// <summary>Actual click position in SVG viewBox coordinates.</summary>
    public double? ClickViewBoxX { get; set; }
    public double? ClickViewBoxY { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
