using Microsoft.AspNetCore.Mvc.Testing;
using SGBackend;

namespace SGBackendTest;

public class UnitTest1 : IClassFixture<WebApplicationFactory<Startup>>
{
    private readonly WebApplicationFactory<Startup> _factory;

    public UnitTest1(WebApplicationFactory<Startup> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async void Test1()
    {
        // Arrange
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/gets");
        var st = await resp.Content.ReadAsStringAsync();
        Assert.Equal("test", st);
    }
}