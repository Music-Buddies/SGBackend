using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SGBackend;
using SGBackend.Models;
using SGBackend.Provider;
using SGBackend.Service;

namespace SGBackendTest.Controllers;

public class UserControllerTest : IClassFixture<WebApplicationFactory<Startup>>
{
    private readonly WebApplicationFactory<Startup> _factory;

    public UserControllerTest(WebApplicationFactory<Startup> factory)
    {
        _factory = factory;
    }


    [Fact]
    public async void SpotifyDisconnect()
    {
        var client = await TestSetupAsync();
        var resp = await client.DeleteAsync("/user/spotify-disconnect");
        Assert.True(resp.IsSuccessStatusCode);
    }

    [Fact]
    public async void GetProfileInformation()
    {
        var client = await TestSetupAsync();
        var response = await client.GetAsync("/user/profile-information");
        var profileInformation = await response.Content.ReadFromJsonAsync<ProfileInformation>();

        Assert.NotNull(profileInformation);
        Assert.NotNull(profileInformation.username);
        Assert.NotNull(profileInformation.profileImage);
        Assert.NotNull(profileInformation.trackingSince);
    }

    [Fact]
    public async void GetPersonalSummary()
    {
        var client = await TestSetupAsync();
        var response = await client.GetAsync("/user/spotify/personal-summary?limit=10");
        var mediaSummariesArray = await response.Content.ReadFromJsonAsync<RecommendedMedia[]>();

        Assert.NotEmpty(mediaSummariesArray);
        Assert.NotEmpty(mediaSummariesArray[0].albumImages);
        Assert.NotNull(mediaSummariesArray[0].albumImages[0].Id);
        Assert.NotEqual(0, mediaSummariesArray[0].albumImages[0].height);
        Assert.NotEqual(0, mediaSummariesArray[0].albumImages[0].width);
        Assert.NotNull(mediaSummariesArray[0].albumImages[0].imageUrl);
        Assert.NotNull(mediaSummariesArray[0].albumName);
        Assert.NotEmpty(mediaSummariesArray[0].allArtists);
        Assert.NotNull(mediaSummariesArray[0].explicitFlag);
        Assert.NotNull(mediaSummariesArray[0].linkToMedia);
        Assert.NotEqual(0, mediaSummariesArray[0].listenedSeconds);
        Assert.NotNull(mediaSummariesArray[0].releaseDate);
        Assert.NotNull(mediaSummariesArray[0].songTitle);
    }

    [Fact]
    public async void GetMatches()
    {
        var client = await TestSetupAsync();
        var response = await client.GetAsync("/user/matches?limit=10");
        var matchesArray = await response.Content.ReadFromJsonAsync<Match[]>();

        Assert.NotEmpty(matchesArray);
        Assert.NotNull(matchesArray[0].username);
        Assert.NotNull(matchesArray[0].userId);
        Assert.NotEqual(0, matchesArray[0].listenedTogetherSeconds);
        Assert.NotEqual(0, matchesArray[0].rank);
        Assert.Equal("1", matchesArray[0].rank.ToString());
        Assert.True(matchesArray[0].rank < matchesArray[1].rank);
    }

    [Fact]
    public async void GetRecommendedMedia()
    {
        var client = await TestSetupAsync();
        var matchesResponse = await client.GetAsync("/user/matches");
        var matchesArray = await matchesResponse.Content.ReadFromJsonAsync<Match[]>();
        var guid = matchesArray[0].userId;
        var response = await client.GetAsync($"/user/matches/{guid}/recommended-media");
        var mediaSummariesArray = await response.Content.ReadFromJsonAsync<RecommendedMedia[]>();

        Assert.NotEmpty(mediaSummariesArray);
        Assert.NotEmpty(mediaSummariesArray[0].albumImages);
        Assert.NotNull(mediaSummariesArray[0].albumImages[0].Id);
        Assert.NotEqual(0, mediaSummariesArray[0].albumImages[0].height);
        Assert.NotEqual(0, mediaSummariesArray[0].albumImages[0].width);
        Assert.NotNull(mediaSummariesArray[0].albumImages[0].imageUrl);
        Assert.NotNull(mediaSummariesArray[0].albumName);
        Assert.NotEmpty(mediaSummariesArray[0].allArtists);
        Assert.NotNull(mediaSummariesArray[0].explicitFlag);
        Assert.NotNull(mediaSummariesArray[0].linkToMedia);
        Assert.NotEqual(0, mediaSummariesArray[0].listenedSeconds);
        Assert.NotNull(mediaSummariesArray[0].releaseDate);
        Assert.NotNull(mediaSummariesArray[0].songTitle);
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