namespace Firefly.Cli;

/// <summary>
/// Minimal <c>--key value</c> / <c>--flag</c> argument parser -- deliberately
/// simple rather than pulling in a full CLI framework for a demo app.
/// </summary>
internal sealed class ArgReader
{
    private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Positionals { get; }

    public ArgReader(string[] args)
    {
        var positionals = new List<string>();
        int i = 0;
        while (i < args.Length)
        {
            string arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                string key = arg[2..];
                bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
                _options[key] = hasValue ? args[i + 1] : "true";
                i += hasValue ? 2 : 1;
            }
            else
            {
                positionals.Add(arg);
                i += 1;
            }
        }
        Positionals = positionals;
    }

    public string? Get(string key) => _options.TryGetValue(key, out string? value) ? value : null;

    public string Require(string key) =>
        Get(key) ?? throw new CliUsageException($"Missing required option --{key}");

    public bool Flag(string key) =>
        _options.TryGetValue(key, out string? value) && (value == "true" || bool.TryParse(value, out bool b) && b);
}
