using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SGBackend.Controllers;
using SGBackend.Entities;

namespace SGBackend.Service;

public class TransferService
{
    private readonly ParalellAlgoService _algoService;

    private readonly SgDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UserService _userService;

    public TransferService(SgDbContext dbContext, UserService userService, ParalellAlgoService algoService,
        IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _userService = userService;
        _algoService = algoService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task ImportFromTarget(string targetSuggestBackend, string adminToken)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post,
            targetSuggestBackend + $"/admin/exportUsers?adminToken={adminToken}");

        var httpClient = _httpClientFactory.CreateClient();
        var httpResponseMessage = await httpClient.SendAsync(message);

        // assuming it worked, otherwise we want to force a crash
        var contentStream = await httpResponseMessage.Content.ReadAsStringAsync();
        var export = JsonConvert.DeserializeObject<ExportContainer>(contentStream);

        await ImportUsers(export);
    }

    public async Task ImportUsers(ExportContainer exportContainer)
    {
        // import missing media, requirement of the most basic record entity type
        var existingMediaUrls = await _dbContext.Media.Select(m => m.LinkToMedium).ToArrayAsync();

        var exportMediaToImport =
            exportContainer.media.Where(m => !existingMediaUrls.Contains(m.LinkToMedium)).Select(m => m.ToMedium())
                .ToList();

        await _dbContext.Media.AddRangeAsync(exportMediaToImport);
        await _dbContext.SaveChangesAsync();

        // get link map for other exports
        var mediaLinkMap = (await _dbContext.Media.ToArrayAsync()).ToDictionary(m => m.LinkToMedium, m => m.Id);

        // filter out users that already exist (no need to import them again
        var existingUserSpotifyIds = await _dbContext.User.Select(u => u.SpotifyId).ToArrayAsync();
        var exportUsersToImport =
            exportContainer.users.Where(u => !existingUserSpotifyIds.Contains(u.SpotifyId)).ToList();


        // import the users with all their records
        var dbUsers = exportUsersToImport.Select(u => u.ToUser(mediaLinkMap)).ToArray();
        foreach (var user in dbUsers) await _userService.AddUser(user);

        await _dbContext.User.AddRangeAsync();
        await _dbContext.SaveChangesAsync();

        // calculate everything for the imported users
        foreach (var dbUser in dbUsers) await _algoService.ProcessImport(dbUser.Id);
    }

    public async Task<ExportContainer> ExportUsers()
    {
        // export all media (needed for import later)
        var media = await _dbContext.Media.Include(m => m.Artists).Include(m => m.Images).ToArrayAsync();

        // TODO: remove startswith dummy and and isDummy flag to user table, after migrations are implemented
        // export all users and their records (the rest will be recalculated)
        var users = await _dbContext.User.Where(u => !u.Name.StartsWith("Dummy")).Include(u => u.PlaybackRecords)
            .ThenInclude(pr => pr.Medium).ToArrayAsync();

        return new ExportContainer
        {
            media = media.Select(m => m.ToExportMedium()).ToList(),
            users = users.Select(u => u.ToExportUser()).ToList()
        };
    }
}