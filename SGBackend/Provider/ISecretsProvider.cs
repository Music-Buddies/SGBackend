namespace SGBackend.Provider;

public interface ISecretsProvider
{
    public T GetSecret<T>();

}