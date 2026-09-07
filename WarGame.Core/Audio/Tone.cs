namespace WarGame.Core.Audio;

/// <summary>A receita de um timbre: as notas por que passa, quanto dura cada uma, os harmónicos que se somam
/// ao seno base, quanto ruído leva por cima, para que lado o tom anda (o "bend" da sirene) e a que velocidade
/// morre. O <see cref="Db"/> é o peso da voz na mistura — o tambor é para se ouvir, o toque dos avisos
/// correntes é para não incomodar.</summary>
public readonly record struct Voice(string Id, float[] Notes, float Each, float[] Harmonics,
                                    float Noise, float Bend, float Decay, float Db);

/// <summary>A sonoplastia do jogo faz-se em código, sem um único ficheiro de áudio no repositório: somam-se
/// senos, põe-se um envelope por cima e sai PCM de 16 bits. Isto é a parte que não sabe nada do Godot — a
/// tabela de timbres e a conta que os desenha — para ser possível medi-la em testes a sério: que o sino não
/// sai mudo, que a sirene desce mesmo de tom, que a mesma voz dá exactamente os mesmos bytes em toda a gente
/// (o ruído do tambor tem gerador próprio, não o do sistema) e que nada estoira o limite dos 16 bits.
///
/// Quem embrulha isto num AudioStreamWav e o toca é o Sfx, do lado da apresentação.</summary>
public static class Tone
{
    /// <summary>Frequência de amostragem. 22 kHz chega de sobra para avisos e pesa metade de 44.</summary>
    public const int Rate = 22050;

    /// <summary>As vozes do jogo, pelo nome que aparece no painel. A diferença entre uma campainha de
    /// laboratório e um tambor de guerra está toda nesta tabela.</summary>
    public static readonly IReadOnlyDictionary<string, Voice> Bank = new Dictionary<string, Voice>
    {
        //                              notas (Hz)                  cada   harmónicos                    ruído  tom     queda  peso
        ["toque"] = new("toque", new[] { 660f }, 0.07f, new[] { 1f, 0.2f }, 0f, 0f, 14f, -16f),
        ["sino"] = new("sino", new[] { 784f, 1046f }, 0.20f, new[] { 1f, 0.5f, 0.25f }, 0f, 0f, 5f, -9f),
        ["campainha"] = new("campainha", new[] { 1318f }, 0.55f, new[] { 1f, 0.6f, 0.4f, 0.2f }, 0f, 0f, 6f, -9f),
        ["tambor"] = new("tambor", new[] { 98f, 73f }, 0.22f, new[] { 1f, 0.35f }, 0.45f, -0.15f, 9f, -7f),
        ["sirene"] = new("sirene", new[] { 880f, 660f }, 0.40f, new[] { 1f, 0.4f }, 0f, -0.25f, 1.5f, -6f),
        ["moeda"] = new("moeda", new[] { 1174f, 1567f }, 0.09f, new[] { 1f, 0.3f }, 0f, 0f, 12f, -12f),
        ["fanfarra"] = new("fanfarra", new[] { 523f, 659f, 784f }, 0.16f, new[] { 1f, 0.45f, 0.2f }, 0f, 0f, 4f, -9f),
    };

    /// <summary>Quantas amostras dá uma voz (uma por nota, seguidas).</summary>
    public static int Samples(Voice v) => v.Notes.Length * (int)(Rate * v.Each);

    /// <summary>Quanto tempo soa, em segundos.</summary>
    public static float Seconds(Voice v) => Samples(v) / (float)Rate;

    /// <summary>Desenha a voz: PCM de 16 bits, mono, little-endian — o formato que o AudioStreamWav come.
    /// A conta é sempre a mesma para os mesmos dados de entrada, ruído incluído, para o jogo soar igual em
    /// todas as máquinas.</summary>
    public static byte[] Pcm16(Voice v)
    {
        int n = (int)(Rate * v.Each);
        var pcm = new byte[v.Notes.Length * n * 2];
        float scale = v.Harmonics.Sum() + v.Noise;
        int k = 0;
        uint seed = (uint)Rate;                                  // gerador próprio: o mesmo ruído em toda a gente
        foreach (float f0 in v.Notes)
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = f0 * (1f + v.Bend * (t / v.Each));     // sirene e tambor descem de tom pelo caminho
                float s = 0f;
                for (int h = 0; h < v.Harmonics.Length; h++)
                    s += v.Harmonics[h] * MathF.Sin(MathF.Tau * f * (h + 1) * t);
                if (v.Noise > 0f)
                {
                    seed = seed * 1664525u + 1013904223u;
                    s += v.Noise * ((seed >> 16 & 0xFFFF) / 32768f - 1f);
                }
                float env = MathF.Min(1f, t / 0.006f) * MathF.Exp(-v.Decay * t);   // ataque curto, queda longa
                short a = (short)Math.Clamp(s * env / scale * 22000f, short.MinValue, short.MaxValue);
                pcm[k++] = (byte)(a & 0xFF);
                pcm[k++] = (byte)((a >> 8) & 0xFF);
            }
        return pcm;
    }

    /// <summary>Lê uma amostra do PCM (é o que os testes usam para medir o que saiu).</summary>
    public static short Sample(byte[] pcm, int i) => (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
}
