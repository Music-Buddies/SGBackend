using System.Text.Json.Serialization;
using SGBackend.Entities;

namespace SGBackend.Models;

public class UserSettings
{
    public string? language { get; set; }
}