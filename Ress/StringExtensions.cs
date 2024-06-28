using ReverseMarkdown;

namespace Ress;

public static class StringExtensions
{
    private static readonly Converter Converter = new(new Config
    {
        UnknownTags = Config.UnknownTagsOption.Bypass,
        RemoveComments = true,
        SmartHrefHandling = true
    });

    public static string ToMarkdown(this string str)
    {
        return Converter.Convert(str);
    }
}