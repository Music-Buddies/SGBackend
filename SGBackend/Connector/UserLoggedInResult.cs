using SGBackend.Entities;

namespace SGBackend.Connector;

/// <summary>
/// The regarding content connector (yt, spotify, soundcloud, ...) is responsible for handling user registration and login
/// </summary>
public class UserLoggedInResult
{
    public User User { get; set; }

    public bool ExistedPreviously { get; set; }
}