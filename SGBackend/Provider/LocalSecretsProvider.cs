namespace SGBackend.Provider;

public class LocalSecretsProvider : ISecretsProvider 
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