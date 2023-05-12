using Refit;
using SecretsProvider;

namespace SGBackend.Models;

[Section("SG")]
public class Secrets
{
    public string JwtKey { get; set; }
    public string SpotifyClientId { get; set; }
    public string SpotifyClientSecret { get; set; }
    public string DBConnectionString { get; set; }
    public string AdminToken { get; set; }
    public string? InitializeTargetToken { get; set; }
    public string? InitializeFromTarget { get; set; }
}