namespace CheckRide.Models;

public class SupabaseSession
{
    public string   AccessToken  { get; set; } = "";
    public string   RefreshToken { get; set; } = "";
    public string   UserId       { get; set; } = "";
    public string   Email        { get; set; } = "";
    public DateTime ExpiresAt    { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
}
