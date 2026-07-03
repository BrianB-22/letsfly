namespace CheckRide;

internal static class Config
{
    public const string SupabaseUrl     = "https://blxugbnoitkxhdjgfkff.supabase.co";
    public const string SupabaseAnonKey = "sb_publishable_LiOn1DINkHtyZOT-V6Ey2Q_OklVWUdY";

    // Minimum distance from departure to consider the flight complete (parking brake at different airport)
    public const double CompletionDistanceNm = 0.5;
}
