using System.ServiceModel.Syndication;
using System.Text;
using System.Timers;
using System.Xml;
using System.Xml.Linq;
using Discord;
using Discord.Webhook;
using Timer = System.Timers.Timer;

namespace Ress;

internal static class Program
{
    private static string _feedUri;
    private static DiscordWebhookClient _webhookClient;
    private static Timer _timer;
    private static DateTimeOffset _lastUpdatedTime;

    private static async Task Main()
    {
        _feedUri = Environment.GetEnvironmentVariable("RESS_FEED_URI");

        _webhookClient = new DiscordWebhookClient(Environment.GetEnvironmentVariable("RESS_WEBHOOK_URI"));
        _webhookClient.Log += message =>
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        };

        _timer = new Timer(TimeSpan.FromSeconds(5));
        _timer.Elapsed += UpdateFeed;
        _timer.Start();

        _lastUpdatedTime = DateTimeOffset.MinValue;

        Console.WriteLine("Client initialized.\nFeed URI: {0}\nTimer Interval: {1}",
            _feedUri,
            TimeSpan.FromMilliseconds(_timer.Interval).ToString());

        await Task.Delay(Timeout.Infinite);
    }

    private static void UpdateFeed(object? sender, ElapsedEventArgs e)
    {
        var xmlReader = XmlReader.Create(_feedUri);
        var syndicationFeed = SyndicationFeed.Load(xmlReader);
        var lastUpdatedTime = syndicationFeed.LastUpdatedTime;

        if (_lastUpdatedTime.CompareTo(lastUpdatedTime) >= 0) return;

        Console.WriteLine("Detected a feed update.");
        SendMessage(syndicationFeed);

        _lastUpdatedTime = lastUpdatedTime;
    }

    private static void SendMessage(SyndicationFeed syndicationFeed)
    {
        var embeds = CreateMessage(syndicationFeed);

        if (embeds.Count != 0)
        {
            _webhookClient.SendMessageAsync(embeds: embeds);
            Console.WriteLine($"Sent {embeds.Count} new items.");
        }
        else
        {
            Console.WriteLine("Sent no new items.");
        }
    }

    private static List<Embed> CreateMessage(SyndicationFeed syndicationFeed)
    {
        var embeds = new List<Embed>();

        foreach (var item in syndicationFeed.Items)
        {
            if (_lastUpdatedTime.CompareTo(item.PublishDate) >= 0) continue;

            var descriptionBuilder = new StringBuilder();

            foreach (var extension in item.ElementExtensions)
            {
                var element = extension.GetObject<XElement>();

                if (element.Name.LocalName == "encoded" && element.Name.Namespace.ToString().Contains("content"))
                    descriptionBuilder.Append(element.Value);
            }

            embeds.Add(new EmbedBuilder
            {
                Title = (item.Title?.Text ?? "No Title.").ToMarkdown(),
                Description = descriptionBuilder.ToString().ToMarkdown(),
                Url = item.Links.FirstOrDefault()?.Uri?.ToString(),
                Timestamp = item.PublishDate,
                Color = new Color(4, 99, 115),
                Footer = new EmbedFooterBuilder().WithText(item.Categories.FirstOrDefault()?.Name ?? "")
            }.Build());
        }

        return embeds;
    }
}