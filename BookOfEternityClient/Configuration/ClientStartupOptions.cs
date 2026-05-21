namespace BookOfEternityClient.Configuration;

public sealed record ClientStartupOptions(bool WebMode, string BasePath, string WebUrl)
{
    public const string DefaultWebUrl = "http://127.0.0.1:8787";

    public static ClientStartupOptions Parse(IReadOnlyList<string> args, string defaultBasePath)
    {
        var webMode = false;
        var basePath = defaultBasePath;
        var webUrl = DefaultWebUrl;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--web", StringComparison.OrdinalIgnoreCase))
            {
                webMode = true;
                continue;
            }

            if (string.Equals(arg, "--web-url", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Count)
                    throw new ArgumentException("Missing value for --web-url.", nameof(args));

                webUrl = args[++index];
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal) && Directory.Exists(arg))
                basePath = arg;
        }

        return new ClientStartupOptions(webMode, basePath, webUrl);
    }
}
