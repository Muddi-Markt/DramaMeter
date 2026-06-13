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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
