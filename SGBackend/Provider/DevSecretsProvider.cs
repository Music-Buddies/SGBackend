using SGBackend.Models;

namespace SGBackend.Provider;

public class DevSecretsProvider : ISecretsProvider
{
    private readonly IConfiguration _configuration;
    
    public DevSecretsProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public T GetSecret<T>()
    {
        var secret = _configuration.GetSection("SG").Get<T>();
        return secret;
    }
}