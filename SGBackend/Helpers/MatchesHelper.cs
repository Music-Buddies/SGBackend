using SGBackend.Entities;
using SGBackend.Models;

namespace SGBackend.Helpers
{
    public static class MatchesHelper
    {
        public static Match[] CreateMatchesArray(IEnumerable<IGrouping<User, MutualPlaybackOverview>> matches)
        {
            if (!matches.Any()) return Array.Empty<Match>();

            var matchesArray = matches.Select(m => new Match
            {
                username = m.Key.Name,
                userId = m.Key.Id.ToString(),
                profileUrl = m.Key.SpotifyProfileUrl,
                listenedTogetherSeconds = m.Sum(o => o.MutualPlaybackEntries.Sum(e => e.PlaybackSeconds)),
            }).OrderByDescending(m => m.listenedTogetherSeconds).Where(m => m.listenedTogetherSeconds != 0).ToArray();

            if (matchesArray.Length == 0) return matchesArray;

            for (int i = 0; i < matchesArray.Length; i++)
            {               
                matchesArray[i].rank = i + 1;
            }
            return matchesArray;
        }
    }
}
