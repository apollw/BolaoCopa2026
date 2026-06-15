using BolaoCopa2026.Models;

namespace BolaoCopa2026;

public sealed record ParticipantAvatarVisual(string Key, string Label, string Kind, string? ImageUrl = null, string? Symbol = null);

public static class ParticipantAvatarCatalog
{
    public static ParticipantAvatarVisual Resolve(string? key)
    {
        var normalized = string.IsNullOrWhiteSpace(key) ? "none" : key.Trim();

        return normalized.ToLowerInvariant() switch
        {
            "flag-bra" => new ParticipantAvatarVisual("flag-bra", "Brasil", "team", TeamDisplay.GetFlagUrl("BRA")),
            "emoji-ball" => new ParticipantAvatarVisual("emoji-ball", "Bola", "emoji", Symbol: "⚽"),
            "emoji-trophy" => new ParticipantAvatarVisual("emoji-trophy", "Taça", "emoji", Symbol: "🏆"),
            "emoji-whistle" => new ParticipantAvatarVisual("emoji-whistle", "Apito", "emoji", Symbol: "🪈"),
            "emoji-goal" => new ParticipantAvatarVisual("emoji-goal", "Gol", "emoji", Symbol: "🥅"),
            "emoji-fire" => new ParticipantAvatarVisual("emoji-fire", "Goleador", "emoji", Symbol: "🔥"),
            "emoji-clap" => new ParticipantAvatarVisual("emoji-clap", "Torcida", "emoji", Symbol: "👏"),
            "emoji-shield" => new ParticipantAvatarVisual("emoji-shield", "Escudo", "emoji", Symbol: "🛡"),
            "emoji-star" => new ParticipantAvatarVisual("emoji-star", "Craque", "emoji", Symbol: "⭐"),
            _ => new ParticipantAvatarVisual("none", "Sem avatar", "emoji", Symbol: "👤")
        };
    }

    public static ParticipantAvatarVisual Resolve(Participant? participant)
    {
        return Resolve(participant?.AvatarKey);
    }
}
