namespace SGBackend.Provider;

public class SecretsProvider : ISecretsProvider 
{
    public string GetSecret(string secretName)
    {
        var dict = new Dictionary<string, string>()
        {
            { "jwt-key", "testsdgfszg4ewrsujhdetrzhtdersyzrezsertdhzgrfdh" }
        };
        return dict[secretName];
    }
}