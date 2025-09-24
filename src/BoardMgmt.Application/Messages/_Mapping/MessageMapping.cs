namespace BoardMgmt.Application.Messages._Mapping;

internal static class MessageMapping
{
    public static string Preview(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        var x = body.Replace("\r", " ").Replace("\n", " ");
        return x.Length > 140 ? x[..140] + "…" : x;
    }
}
