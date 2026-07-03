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

    public string DisplayRoute  => $"{DepId} → {ArrId}";
    public string DisplayFlight => $"{DepName} → {ArrName}";
    public string DisplayDate   => CreatedAt.ToLocalTime().ToString("MMM d, yyyy");

    public int DistanceNm
    {
        get
        {
            const double R = 3440.065; // Earth radius in NM
            var dLat = (ArrLat - DepLat) * Math.PI / 180;
            var dLon = (ArrLon - DepLon) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(DepLat * Math.PI / 180) * Math.Cos(ArrLat * Math.PI / 180)
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return (int)Math.Round(R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)));
        }
    }
}
