namespace SGBackend.Provider;

public class LocalSecretsProvider : ISecretsProvider 
{
    public string GetSecret(string secretName)
    {
        Environment.GetEnvironmentVariable("jwt-key");
        
        var dict = new Dictionary<string, string>()
        {
            { "jwt-key", "testsdgfszg4ewrsujhdetrzhtdersyzrezsertdhzgrfdh" }
        };
        return dict[secretName];
    }
}