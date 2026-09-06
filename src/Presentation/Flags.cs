using Godot;

namespace WarGame.Presentation;

/// <summary>Bandeiras dos países (assets/flags/&lt;TAG&gt;.png, geradas por tools/flag_colors.py).
/// Cache por tag; países sem bandeira (ex.: Somalilândia) devolvem null e a UI esconde o quadro.</summary>
public static class Flags
{
    private static readonly Dictionary<string, Texture2D?> Cache = new();

    public static Texture2D? Of(string tag)
    {
        if (Cache.TryGetValue(tag, out var t)) return t;
        var path = $"res://assets/flags/{tag}.png";
        t = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        return Cache[tag] = t;
    }

    /// <summary>TextureRect pronto para uma linha de UI (mantém proporção, altura ~h px).</summary>
    public static TextureRect Rect(float h = 26)
        => new()
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(h * 1.5f, h),
        };
}
