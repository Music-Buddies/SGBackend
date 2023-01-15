using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGBackend;
using SGBackend.Models;
using SGBackend.Provider;
using SGBackend.Service;
using System.Net.Http.Json;

namespace SGBackendTest.Controllers;

public class UserControllerTest : IClassFixture<WebApplicationFactory<Startup>>
{
    private readonly WebApplicationFactory<Startup> _factory;

    public UserControllerTest(WebApplicationFactory<Startup> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async void GetMatches()
    {
        var client = await TestSetupAsync();
        var response = await client.GetAsync("/user/matches");
        var matchesArray = await response.Content.ReadFromJsonAsync<Match[]>();

        Assert.NotEmpty(matchesArray);
        Assert.NotNull(matchesArray[0].username);
        Assert.NotNull(matchesArray[0].userId);
        Assert.NotEqual(0, matchesArray[0].listenedTogetherSeconds);
        Assert.NotEqual(0, matchesArray[0].rank);
        Assert.Equal("1", matchesArray[0].rank.ToString());
        Assert.True(matchesArray[0].rank < matchesArray[1].rank);
    }

    private async Task<HttpClient> TestSetupAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var scopedRandomizedUserService = scope.ServiceProvider.GetService<RandomizedUserService>();
        var scopedJwtProvider = scope.ServiceProvider.GetService<JwtProvider>();
        var users = await scopedRandomizedUserService.GenerateXRandomUsersAndCalc(2);
        var token = scopedJwtProvider.GetJwt(users.FirstOrDefault());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

        return client;
    }
}