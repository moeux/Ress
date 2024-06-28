using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Discord;
using Discord.Webhook;

namespace Ress;

internal static class Program
{
    private static DiscordWebhookClient _client;

    private static async Task Main()
    {
        _client = new DiscordWebhookClient(Environment.GetEnvironmentVariable("RESS_WEBHOOK_URI"));

        _client.Log += message =>
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        };

        await _client.SendMessageAsync(embeds: CreateMessage());
        await Task.Delay(Timeout.Infinite);
    }

    private static List<Embed> CreateMessage()
    {
        var embeds = new List<Embed>();
        var xmlReader = XmlReader.Create("https://www.netcup-sonderangebote.de/feed");
        var syndicationFeed = SyndicationFeed.Load(xmlReader);

        foreach (var item in syndicationFeed.Items)
        {
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