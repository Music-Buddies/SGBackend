using System.Text.Json;
using SGBackend.Connector;

namespace SGBackendTest;

public class RandomizedHistoryService
{
    public SpotifyListenHistory GetRandomizedHistory()
    {
        var listenHistory = JsonSerializer.Deserialize<SpotifyListenHistory>(File.ReadAllText("fullListenHistory.json"));
        
        var rnd = new Random();
        // get random tracks
        var randomItems = listenHistory.items.OrderBy(x => rnd.Next()).Take(10).ToList();
        // randomize listen times and listen duration
        
        foreach (var randomItem in randomItems)
        {
            // randomize listen duration and played at timestamp
            var randomFactor = rnd.Next(-100, 100) / 100.0D;
            randomItem.track.duration_ms = (int) (randomItem.track.duration_ms * randomFactor);
            randomItem.played_at = randomItem.played_at.AddDays(rnd.Next(-100, 100));
        }

        return new SpotifyListenHistory()
        {
            items = randomItems
        };
    }
}