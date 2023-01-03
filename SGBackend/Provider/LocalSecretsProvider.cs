namespace SGBackend.Provider;

public class LocalSecretsProvider : ISecretsProvider 
{
    public string GetSecret(string secretName)
    {

        var dict = new Dictionary<string, string>()
        {
            { "jwt-key", "testsdgfszg4ewrsujhdetrzhtdersyzrezsertdhzgrfdh" },
            {"spotify_client_id", "de22eb2cc8c9478aa6f81f401bcaa23a"},
            {"spofity_client_secret", "03e25493374146c987ee581f6f64ad1f"}
        };
        return dict[secretName];
    }
}