using SGBackend.Models;

namespace SGBackend.Connector;



public class SpotifyListenHistory
{
    public List<Item> items { get; set; }
    public string? next { get; set; }
    public Cursors cursors { get; set; }
    public int limit { get; set; }
    public string href { get; set; }

    public HashSet<Media> GetMedia()
    {
        return items.Select(item => new Media()
        {
            Title = item.track.name,
            MediaSource = MediaSource.Spotify,
            LinkToMedia = item.track.href,
            ExplicitContent = item.track.@explicit,
            Artists = item.track.artists.Select(a => new SGBackend.Models.Artist()
            {
                Name = a.name
            }).ToList(),
            Images = item.track.album.images.Select(i => new MediaImage()
            {
                height = i.height,
                imageUrl= i.url,
                width = i.width
            }).ToList()
        }).ToHashSet();
    }

    public List<PlaybackRecord> GetPlaybackRecords(Media[] existingMediaSpotify, User user)
    {
        return items.Select(item => new PlaybackRecord()
        {
            Media = existingMediaSpotify.First(media => media.MediaSource == MediaSource.Spotify && media.LinkToMedia == item.track.href),
            PlayedAt = item.played_at,
            PlayedSeconds = item.track.duration_ms,
            User = user
        }).ToList();
    }
    
}

public class Album
{
    public string album_type { get; set; }
    public List<Artist> artists { get; set; }
    public List<string> available_markets { get; set; }
    public ExternalUrls external_urls { get; set; }
    public string href { get; set; }
    public string id { get; set; }
    public List<Image> images { get; set; }
    public string name { get; set; }
    public string release_date { get; set; }
    public string release_date_precision { get; set; }
    public int total_tracks { get; set; }
    public string type { get; set; }
    public string uri { get; set; }
}

public class Artist
{
    public ExternalUrls external_urls { get; set; }
    public string href { get; set; }
    public string id { get; set; }
    public string name { get; set; }
    public string type { get; set; }
    public string uri { get; set; }
}

public class Context
{
    public ExternalUrls external_urls { get; set; }
    public string href { get; set; }
    public string type { get; set; }
    public string uri { get; set; }
}

public class Cursors
{
    public string after { get; set; }
    public string before { get; set; }
}

public class ExternalIds
{
    public string isrc { get; set; }
}

public class ExternalUrls
{
    public string spotify { get; set; }
}

public class Image
{
    public int height { get; set; }
    public string url { get; set; }
    public int width { get; set; }
}

public class Item
{
    public Track track { get; set; }
    public DateTime played_at { get; set; }
    public Context context { get; set; }
}

public class Track
{
    public Album album { get; set; }
    public List<Artist> artists { get; set; }
    public List<string> available_markets { get; set; }
    public int disc_number { get; set; }
    public int duration_ms { get; set; }
    public bool @explicit { get; set; }
    public ExternalIds external_ids { get; set; }
    public ExternalUrls external_urls { get; set; }
    public string href { get; set; }
    public string id { get; set; }
    public bool is_local { get; set; }
    public string name { get; set; }
    public int popularity { get; set; }
    public string preview_url { get; set; }
    public int track_number { get; set; }
    public string type { get; set; }
    public string uri { get; set; }
}