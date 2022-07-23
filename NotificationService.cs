using HtmlAgilityPack;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using System.Text.Json;

class NotificationService : IHostedService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IConfiguration _env;
    private readonly HttpClient _httpClient;
    private readonly PeriodicTimer _timer;
    private readonly IMongoCollection<Movie> _collection;

    public NotificationService(IConfiguration env, ILogger<NotificationService> logger, IMongoClient mongoClient, HttpClient httpClient)
    {
        _logger = logger;
        _env = env;
        _httpClient = httpClient;
        _collection = mongoClient.GetDatabase("scraper").GetCollection<Movie>("movies");
        _timer = new PeriodicTimer(TimeSpan.FromHours(Convert.ToDouble(env[Interval.ScrapingInterval.ToString()])));
       
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (await _timer.WaitForNextTickAsync(cancellationToken))
        {
            var html = @"https://www.thenetnaija.net/videos/movies";

            HtmlWeb web = new HtmlWeb();

            HtmlDocument htmlDoc = web.Load(html);

            var elements = htmlDoc.DocumentNode.SelectNodes("//div/h2/a");

            var newMovies = new List<Movie>();
            try
            {
                foreach (var node in elements)
                {
                    var link = node.GetAttributes("href").First().Value;
                    var title = node.GetAttributes("href").First().OwnerNode.InnerHtml;
                    var findMovie = await _collection.Find(Builders<Movie>.Filter.Eq(movie => movie.Link, link)).ToListAsync();
                    if (findMovie.Count == 0)
                    {
                        var movie = new Movie(new ObjectId(), title, link, DateTime.Now);
                        newMovies.Add(movie);
                    }

                }
                if (newMovies.Count > 0)
                {
                    await _collection.InsertManyAsync(newMovies);
                    _logger.LogInformation("data saved");
                    var counter = 1;
                    newMovies.ForEach((movie =>
                    {
                        var payload = new
                        {
                            content = $"Title: {movie.Title}\nLink: {movie.Link}\nNo: {counter++}"
                        };
                        var stringifiedContent = JsonSerializer.Serialize(payload);
                        var content = new StringContent(stringifiedContent, Encoding.UTF8, "application/json");
                        var p = _httpClient.PostAsync(_env["webhook"], content).Result;
                        _logger.LogInformation("sent");
                        Task.Delay(1000).Wait();
                    }));


                }
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

