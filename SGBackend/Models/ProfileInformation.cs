using System.Text.Json.Serialization;
using SGBackend.Entities;

namespace SGBackend.Models;

public class ProfileInformation
{
    public string username { get; set; }

    public string profileImage { get; set; }

    public DateTime? trackingSince { get; set; }

    public long totalListenedSeconds { get; set; }
    
    public long? totalTogetherListenedSeconds { get; set; }

    public DateTime? latestFetch { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Language language { get; set; }
}