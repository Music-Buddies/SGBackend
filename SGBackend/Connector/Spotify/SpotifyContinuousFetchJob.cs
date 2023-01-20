using Microsoft.EntityFrameworkCore;
using Quartz;
using SGBackend.Entities;
using SGBackend.Service;

namespace SGBackend.Connector.Spotify;

public class SpotifyContinuousFetchJob : IJob
{
    private readonly SgDbContext _dbContext;
    
    private readonly ISchedulerFactory _schedulerFactory;

    private readonly SpotifyConnector _spotifyConnector;

    private readonly ParalellAlgoService _algoService;

    private readonly ILogger<SpotifyContinuousFetchJob> _logger;
    
    public SpotifyContinuousFetchJob(SgDbContext dbContext, ISchedulerFactory schedulerFactory,
        SpotifyConnector spotifyConnector, ParalellAlgoService algoService, ILogger<SpotifyContinuousFetchJob> logger)
    {
        _dbContext = dbContext;
        _schedulerFactory = schedulerFactory;
        _spotifyConnector = spotifyConnector;
        _algoService = algoService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var userId = context.MergedJobDataMap.GetGuidValue("userId");
        var initialJob = context.MergedJobDataMap.GetBooleanValue("isInitialJob");
        _logger.LogInformation("executing job: " + string.Join(", ", context.MergedJobDataMap));

        var dbUser = await _dbContext.User.Where(u => u.Id == userId).FirstAsync();
        var availableHistory = await _spotifyConnector.FetchAvailableContentHistory(dbUser);

        // only fetch if its not the intial job (on startup also fetches on login)
        if (!initialJob)
        {
            await _algoService.Process(dbUser.Id, availableHistory);
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        var nextFetchJob = JobBuilder.Create<SpotifyContinuousFetchJob>()
            .UsingJobData("userId", userId)
            .UsingJobData("isInitialJob", false)
            .Build();

        if (availableHistory.items.Count < 2)
        {
            // user not active on spotify, reschedule for in a week
            var offset = DateTimeOffset.Now.AddMonths(1);
            var oneMonthTrigger = TriggerBuilder.Create()
                .StartAt(offset)
                .Build();
            _logger.LogInformation("sheduling for: " + offset);
            await scheduler.ScheduleJob(nextFetchJob, oneMonthTrigger);
            return;
        }
        // more records => calculate time it took to generate and formulate new trigger

        var mostRecentRecord = availableHistory.items.First();
        var oldestRecord = availableHistory.items.Last();

        // quarter of time it took to generate to fetch again, rough estimate
        var timeToProduceRecords = (mostRecentRecord.played_at - oldestRecord.played_at) * 0.25;
        var jobStartDate = DateTimeOffset.Now.Add(timeToProduceRecords);
        _logger.LogInformation("sheduling for: " + jobStartDate);
        var calculatedTrigger = TriggerBuilder.Create()
            .StartAt(jobStartDate)
            .Build();

        await scheduler.ScheduleJob(nextFetchJob, calculatedTrigger);
    }
}