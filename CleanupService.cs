
using MongoDB.Driver;

public class CleanupService : IHostedService
    {
    private readonly ILogger<CleanupService> _logger;
    private readonly IConfiguration _env;
    private readonly PeriodicTimer _timer;
    private readonly IMongoCollection<Movie> _collection;
    public CleanupService(IConfiguration env, ILogger<CleanupService> logger, IMongoClient mongoClient)
    {
        _logger = logger;
        _env = env;
        _collection = mongoClient.GetDatabase("scraper").GetCollection<Movie>("movies");
        _timer = new PeriodicTimer(TimeSpan.FromHours(Convert.ToDouble(env[Interval.CleanupInterval.ToString()])));

    }
    public async Task StartAsync(CancellationToken cancellationToken)
        {
        while (await _timer.WaitForNextTickAsync())
        {
            try
            {
                
                var deletedMovies = await _collection.DeleteManyAsync(Builders<Movie>.Filter.Lt(movie => movie.Time, DateTime.Now));
                _logger.LogInformation($"Number of movies deleted: ${deletedMovies.DeletedCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.GetType().ToString());
                _logger.LogError(ex.Message);
                _logger.LogError(ex.StackTrace);
            }
           
        }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
        _logger.LogInformation("shutting down......");
        return Task.CompletedTask;
    }
    }

