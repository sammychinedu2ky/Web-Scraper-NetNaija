using HtmlAgilityPack;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<NotificationService>();
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddHostedService<CleanupService>();
builder.Services.AddSingleton(s =>
{
    var logger = s.GetService<ILogger<NotificationService>>()!;
    var env = s.GetService<IConfiguration>()!;
    var httpClient = s.GetService<HttpClient>()!;
    return new SendMessages(logger, env, httpClient);
});

builder.Services.AddSingleton<IMongoClient>(s =>
{
    var uri = s.GetRequiredService<IConfiguration>()["MongoUri"];
    return new MongoClient(uri);
});
var app = builder.Build();
app.Run();
record Movie(ObjectId Id, string Title, string Link, DateTime Time);
enum Interval
{
    CleanupInterval,
    ScrapingInterval
}



