namespace SGBackend.Provider;

public interface ISecretsProvider
{
    public string GetSecret(string secretName);
}