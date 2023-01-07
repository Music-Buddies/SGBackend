using System.Text.Json;

namespace SGBackend.Provider;

public class EnvSecretsProvider : ISecretsProvider
{
    public T GetSecret<T>()
    {
        var secretName = typeof(T).Name;
        var secretJsonFromEnv = Environment.GetEnvironmentVariable("SG_" + secretName.ToUpper());
        if (string.IsNullOrEmpty(secretJsonFromEnv))
        {
            throw new Exception("Env var for secret " + "SG_" + secretName.ToUpper() + " not set!");
        }

        var secret = JsonSerializer.Deserialize<T>(secretJsonFromEnv);
        if (secret == null)
        {
            throw new Exception("Error deserializing secret from env: " + secretName + " " + secretJsonFromEnv);
        }
        return secret;
    }
}