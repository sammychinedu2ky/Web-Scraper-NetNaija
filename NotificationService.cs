using HtmlAgilityPack;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using System.Text.Json;

class NotificationService : IHostedService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IConfiguration _env;
    private readonly PeriodicTimer _timer;
    private readonly IMongoCollection<Movie> _collection;
    private readonly SendMessages _sendClient;

    public NotificationService(IConfiguration env, ILogger<NotificationService> logger, IMongoClient mongoClient, SendMessages sendClient)
    {
        _logger = logger;
        _env = env;
        _collection = mongoClient.GetDatabase("scraper").GetCollection<Movie>("movies");
        _timer = new PeriodicTimer(TimeSpan.FromHours(Convert.ToDouble(env[Interval.ScrapingInterval.ToString()])));
        _sendClient = sendClient;
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var html = @"https://www.thenetnaija.net/videos/movies";

        HtmlWeb web = new HtmlWeb();

        try
        {
            while (await _timer.WaitForNextTickAsync(cancellationToken))
            {

                HtmlDocument htmlDoc = web.Load(html);

                var elements = htmlDoc.DocumentNode.SelectNodes("//div/h2/a");

                var newMovies = new List<Movie>();
                foreach (var node in elements)
                {
                    var link = node.GetAttributes("href").First().Value;
                    var title = node.GetAttributes("href").First().OwnerNode.InnerHtml;
                    var findMovie = await _collection.Find(Builders<Movie>.Filter.Eq(movie => movie.Link, link)).FirstOrDefaultAsync();
                    if (findMovie is null)
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
                            content = $"No: **{counter++}**\nTitle: **{movie.Title}**\nLink: {movie.Link}"
                        };

                        _sendClient.Send(payload);

                    }));


                }

            }
        }
        catch (Exception ex)
        {
            dynamic payload;
            if (ex is OperationCanceledException)
            {
                payload = new
                {
                    content = $"ErrorType: **ShuttingDown**\nErrorMessage: **ShuttingDown**\nErrorStackTrace: **ShuttingDown**"
                };
                _sendClient.Send(payload).Wait();
            }
            else
            {
                var errorType = ex.GetType().ToString();
                var errorMessage = ex.Message;
                var errorStackTrace = ex.StackTrace;
                _logger.LogError(errorType);
                _logger.LogError(errorMessage);
                _logger.LogError(errorStackTrace);
                payload = new
                {
                    content = $"ErrorType: **{errorType}**\nErrorMessage: **{errorMessage}**\nErrorStackTrace: **{errorStackTrace}**"
                };
                _sendClient.Send(payload).Wait();

            }
        }

    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("shutting down......");
        return Task.CompletedTask;
    }
}

class SendMessages
{
    public IConfiguration _env { get; set; } = default!;
    public ILogger<NotificationService> _logger { get; set; } = default!;
    public HttpClient _httpClient;
    public SendMessages(ILogger<NotificationService> logger, IConfiguration env, HttpClient httpClient)
    {
        _logger = logger;
        _env = env;
        _httpClient = httpClient;
    }
    public Task Send(dynamic payload)
    {
        var stringifiedContent = JsonSerializer.Serialize(payload);
        var content = new StringContent(stringifiedContent, Encoding.UTF8, "application/json");
        var send = _httpClient.PostAsync(_env["webhook"], content).Result;
        _logger.LogInformation($"Status Code: {send.StatusCode}");
        Task.Delay(1000).Wait();
        return Task.CompletedTask;
    }
}

