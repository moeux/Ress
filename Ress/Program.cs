﻿using System.ServiceModel.Syndication;
using System.Text;
using System.Timers;
using System.Xml;
using System.Xml.Linq;
using Discord;
using Discord.Webhook;
using Serilog;
using Serilog.Core;
using Timer = System.Timers.Timer;

namespace Ress;

internal static class Program
{
    private static Logger _logger = null!;
    private static string _feedUri = null!;
    private static DiscordWebhookClient _webhookClient = null!;
    private static Timer _timer = null!;
    private static DateTimeOffset _lastUpdatedTime;

    private static async Task Main()
    {
        _logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        if (TryGetEnvironmentVariable("RESS_FEED_URI", out var feedUri))
        {
            _feedUri = feedUri;
        }
        else
        {
            _logger.Fatal("`RESS_FEED_URI` environment variable is not set");
            return;
        }

        if (TryGetEnvironmentVariable("RESS_WEBHOOK_URI", out var webhookUri))
        {
            _webhookClient = new DiscordWebhookClient(webhookUri);
        }
        else
        {
            _logger.Fatal("`RESS_WEBHOOK_URI` environment variable is not set");
            return;
        }

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

    private static bool TryGetEnvironmentVariable(string name, out string value)
    {
        value = Environment.GetEnvironmentVariable(name)!;

        return !string.IsNullOrEmpty(value);
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