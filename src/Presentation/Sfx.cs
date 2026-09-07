using Godot;
using WarGame.Core.Audio;

namespace WarGame.Presentation;

/// <summary>A sonoplastia do jogo, gerada em código. O DefeatKlaxon provou que dá para ter som sem meter um
/// único ficheiro de áudio no repositório: soma-se meia dúzia de senos, faz-se um envelope e o Godot toca.
/// Mas era um som só, e para uma coisa só — o resto do jogo era mudo. Um foco de dez dias acabava, uma
/// investigação chegava ao fim, um país capitulava: tudo em silêncio, com uma linha de texto que passava em
/// quatro segundos.
///
/// Aqui estão as vozes todas, cada uma com o seu timbre — sino, campainha, tambor, sirene, moeda, fanfarra e
/// o toque seco dos avisos correntes — e cada uma sabe soar a coisa diferente: o sino é harmónico e demora a
/// morrer, o tambor é ruído com um seno grave por baixo, a sirene desce de tom. Toca-se por vozes rotativas
/// (AudioStreamPlayer em roda) para dois avisos seguidos não se cortarem um ao outro.
///
/// A conta que desenha as ondas não está aqui — está no WarGame.Core.Audio.Tone, que não conhece o Godot e
/// por isso se pode medir em testes a sério. Aqui só se embrulha o PCM num AudioStreamWav e se toca.
///
/// Não sabe nada do World: recebe o Kind de quem ouve o barramento.</summary>
public partial class Sfx : Node
{
    /// <summary>As vozes do jogo. Cada uma tem nome (Voice) e é por esse nome que se vai buscar a receita
    /// à tabela do Core.</summary>
    public enum Kind { None, Blip, Chime, Bell, Drum, Siren, Coin, Fanfare }

    private const int Voices = 4;                 // avisos seguidos não se cortam uns aos outros

    private readonly Dictionary<Kind, AudioStreamWav> _bank = new();
    private readonly List<AudioStreamPlayer> _players = new();
    private int _next;

    /// <summary>Som desligado (o botão do altifalante na barra de topo).</summary>
    public bool Muted { get; set; }
    /// <summary>O último timbre que tocou e quantos já tocaram (é o que o --smoke conta).</summary>
    public Kind Last { get; private set; } = Kind.None;
    public int Played { get; private set; }

    public override void _Ready()
    {
        foreach (Kind k in Enum.GetValues<Kind>())
            if (k != Kind.None) _bank[k] = Make(k);
        for (int i = 0; i < Voices; i++)
        {
            var p = new AudioStreamPlayer { VolumeDb = -8f };
            AddChild(p);
            _players.Add(p);
        }
    }

    /// <summary>Toca um timbre. Silencioso e sem erro quando o som está desligado ou o banco ainda não foi
    /// feito — a UI não pode parar por causa de um aviso sonoro.</summary>
    public void Play(Kind kind)
    {
        if (kind == Kind.None) return;
        Last = kind; Played++;
        if (Muted || _players.Count == 0 || !_bank.TryGetValue(kind, out var wav)) return;
        var p = _players[_next++ % _players.Count];
        p.Stream = wav;
        p.VolumeDb = Tone.Bank[Voice(kind)].Db;
        p.Play();
    }

    /// <summary>A chapa que vai no cartão do aviso, para o ouvido e o olho dizerem a mesma coisa.</summary>
    public static string Icon(Kind k) => k switch
    {
        Kind.Chime => "🎯",
        Kind.Bell => "🔬",
        Kind.Drum => "⚔",
        Kind.Siren => "🚨",
        Kind.Coin => "🏗",
        Kind.Fanfare => "🎖",
        _ => "•",
    };

    /// <summary>A cor da moldura do cartão do aviso: vermelho no que é mau, latão no que é conquista.</summary>
    public static Color Tint(Kind k) => k switch
    {
        Kind.Siren => Ui.Danger,
        Kind.Drum => Ui.Danger.Darkened(0.25f),
        Kind.Chime => Ui.Good,
        Kind.Bell => new Color(0.55f, 0.75f, 0.95f),
        Kind.Coin => new Color(0.85f, 0.72f, 0.35f),
        Kind.Fanfare => Ui.Accent,
        _ => Ui.Frame,
    };

    /// <summary>Nome de painel do timbre (o --smoke e o botão do som dizem-no por extenso). Chama-se Voice
    /// e não Name porque Node.Name já existe e uma coisa esconderia a outra.</summary>
    public static string Voice(Kind k) => k switch
    {
        Kind.Blip => "toque",
        Kind.Chime => "sino",
        Kind.Bell => "campainha",
        Kind.Drum => "tambor",
        Kind.Siren => "sirene",
        Kind.Coin => "moeda",
        Kind.Fanfare => "fanfarra",
        _ => "nenhum",
    };

    /// <summary>Embrulha a voz da tabela do Core num stream que o Godot toca.</summary>
    private static AudioStreamWav Make(Kind kind) => new()
    {
        Format = AudioStreamWav.FormatEnum.Format16Bits,
        MixRate = Tone.Rate,
        Stereo = false,
        Data = Tone.Pcm16(Tone.Bank[Voice(kind)]),
    };

    /// <summary>--smoke: toca as vozes todas e diz quantas há e quanto tempo somam.</summary>
    public string Smoke()
    {
        float secs = 0f;
        foreach (Kind k in Enum.GetValues<Kind>())
        {
            if (k == Kind.None) continue;
            Play(k);
            if (_bank.TryGetValue(k, out var wav)) secs += wav.Data.Length / 2f / wav.MixRate;
        }
        return $"{_bank.Count} vozes ({string.Join(", ", _bank.Keys.Select(Voice))}) em {secs:0.0}s de áudio gerado, "
             + $"{Played} toques{(Muted ? ", som desligado" : "")}";
    }
}
