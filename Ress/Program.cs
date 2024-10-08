﻿using System.Collections.Immutable;
using System.ServiceModel.Syndication;
using System.Text;
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
    private static DateTimeOffset _lastUpdatedTime;
    private static HashSet<int> _sentEmbedsHashCodes = null!;
    private static readonly object LockObject = new();

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
            _logger.Information("{Message}", message);
            return Task.CompletedTask;
        };

        var timer = new Timer(TimeSpan.FromSeconds(5));
        timer.Elapsed += async (_, _) => await UpdateFeedAsync();
        timer.Start();

        _lastUpdatedTime = DateTimeOffset.MinValue;
        _sentEmbedsHashCodes = [];

        _logger.Information("Client initialized with Feed URI: {FeedUri} and Timer Interval: {TimerInterval} ms",
            _feedUri,
            timer.Interval);

        await Task.Delay(Timeout.Infinite);
    }

    private static bool TryGetEnvironmentVariable(string name, out string value)
    {
        value = Environment.GetEnvironmentVariable(name)!;

        return !string.IsNullOrEmpty(value);
    }

    private static async Task UpdateFeedAsync()
    {
        var xmlReader = XmlReader.Create(_feedUri);
        var syndicationFeed = SyndicationFeed.Load(xmlReader);
        var lastUpdatedTime = syndicationFeed.LastUpdatedTime;

        if (_lastUpdatedTime.CompareTo(lastUpdatedTime) >= 0) return;

        _logger.Information("Detected a feed update");
        await SendMessageAsync(CreateEmbeds(syndicationFeed.Items));

        _lastUpdatedTime = lastUpdatedTime;
    }

    private static List<Embed> CreateEmbeds(IEnumerable<SyndicationItem> items)
    {
        var embeds = new List<Embed>();

        foreach (var item in items)
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
                Title = (item.Title?.Text ?? "No Title.").ToMarkdown().Truncate(EmbedLimit.Title),
                Description = descriptionBuilder.ToString().ToMarkdown().Truncate(EmbedLimit.Description),
                Url = item.Links.FirstOrDefault()?.Uri?.ToString(),
                Timestamp = item.PublishDate,
                Color = new Color(4, 99, 115),
                Footer = new EmbedFooterBuilder().WithText(
                    item.Categories.FirstOrDefault()?.Name.Truncate(EmbedLimit.Footer) ?? "")
            }.Build());
        }

        return embeds;
    }

    private static void AddEmbedsToSentEmbeds(params Embed[] embeds)
    {
        lock (LockObject)
        {
            foreach (var embed in embeds)
                _sentEmbedsHashCodes.Add(embed.GetHashCode());
        }
    }

    private static async Task SendMessageAsync(ICollection<Embed> embeds)
    {
        var embedsToSend = embeds.ExceptBy(_sentEmbedsHashCodes, embed => embed.GetHashCode()).ToImmutableArray();

        if (embedsToSend.Length > 0)
            foreach (var chunk in embedsToSend.Chunk(EmbedLimit.Count))
            {
                if (chunk.Select(embed => embed.Length).Sum() > EmbedLimit.Total)
                    await Task.WhenAll(chunk
                        .Select(async embed => await _webhookClient.SendMessageAsync(embeds: [embed]))
                        .ToArray<Task>());
                else
                    await _webhookClient.SendMessageAsync(embeds: chunk);

                AddEmbedsToSentEmbeds(chunk);
            }

        _logger.Information("Sent {Count} new items", embedsToSend.Length);
    }
}