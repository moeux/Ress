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

    public static string Truncate(this string str, int length, int padding = 2)
    {
        if (string.IsNullOrWhiteSpace(str) || str.Length <= length) return str;

        return str[..(length - padding)].PadRight(padding, '.');
    }
}