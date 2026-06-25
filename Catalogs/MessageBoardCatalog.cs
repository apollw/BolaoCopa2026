namespace BolaoCopa2026.Catalogs;

public sealed record MessageMoodOption(string Key, string Label, string Emoji);

public static class MessageMoodCatalog
{
    public static IReadOnlyList<MessageMoodOption> All { get; } =
    [
        new("entusiasmado", "Entusiasmado", "🚀"),
        new("preocupado", "Preocupado", "😰"),
        new("animado", "Animado", "⚡"),
        new("feliz", "Feliz", "😄"),
        new("festejando", "Festejando", "🎉"),
        new("desanimado", "Desanimado", "😞"),
        new("confuso", "Confuso", "🤔"),
        new("triste", "Triste", "😢"),
        new("confiante", "Confiante", "😎"),
        new("nervoso", "Nervoso", "😬")
    ];

    public static MessageMoodOption? Resolve(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim();
        return All.FirstOrDefault(item => item.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }
}

