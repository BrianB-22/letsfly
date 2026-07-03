using System.Text.Json.Serialization;

namespace CheckRide.Models;

public class SavedFlight
{
    [JsonPropertyName("id")]         public string   Id      { get; set; } = "";
    [JsonPropertyName("dep_id")]     public string   DepId   { get; set; } = "";
    [JsonPropertyName("dep_name")]   public string   DepName { get; set; } = "";
    [JsonPropertyName("dep_lat")]    public double   DepLat  { get; set; }
    [JsonPropertyName("dep_lon")]    public double   DepLon  { get; set; }
    [JsonPropertyName("arr_id")]     public string   ArrId   { get; set; } = "";
    [JsonPropertyName("arr_name")]   public string   ArrName { get; set; } = "";
    [JsonPropertyName("arr_lat")]    public double   ArrLat  { get; set; }
    [JsonPropertyName("arr_lon")]    public double   ArrLon  { get; set; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }

    public string DisplayRoute => $"{DepId} → {ArrId}";
    public string DisplayFlight => $"{DepName} → {ArrName}";
    public string DisplayDate  => CreatedAt.ToLocalTime().ToString("MMM d, yyyy");
}
