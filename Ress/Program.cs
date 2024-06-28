using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Discord;
using Discord.WebSocket;

namespace Ress;

internal static class Program
{
    private static async Task Main()
    {
        var xmlReader = XmlReader.Create("https://www.netcup-sonderangebote.de/feed");
        var syndicationFeed = SyndicationFeed.Load(xmlReader);

        foreach (var item in syndicationFeed.Items)
        {
            var title = (item.Title?.Text ?? "No Title.").ToMarkdown();
            var descriptionBuilder = new StringBuilder();

            foreach (var extension in item.ElementExtensions)
            {
                var element = extension.GetObject<XElement>();
                if (element.Name.LocalName == "encoded" && element.Name.Namespace.ToString().Contains("content"))
                    descriptionBuilder.Append(element.Value.ToMarkdown());
            }

            await SendMessageAsync(title, descriptionBuilder.ToString());
            break;
        }
    }

    private static async Task SendMessageAsync(string title, string description)
    {
        var client = new DiscordSocketClient();
        var token = Environment.GetEnvironmentVariable("RESS_DISCORD_BOT_TOKEN");
        var embed = new EmbedBuilder
        {
            Title = title,
            Description = description,
            Timestamp = DateTimeOffset.Now,
            Color = new Color(4, 99, 115)
        }.Build();

        client.Log += message =>
        {
            Console.WriteLine(message);
            return Task.CompletedTask;
        };

        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();
        await client.GetGuild(950625232446701578).GetTextChannel(1256246245396058202).SendMessageAsync(embed: embed);
        await client.LogoutAsync();
    }
}