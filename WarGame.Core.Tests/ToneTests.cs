using WarGame.Core.Audio;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A sonoplastia gerada em código. O klaxon da derrota provou que dá para ter som sem meter ficheiros
/// de áudio no repositório, mas era um som só; agora há uma caixa de vozes — sino, campainha, tambor, sirene,
/// moeda, fanfarra e o toque seco dos avisos correntes — e cada aviso do jogo toca a sua.
///
/// Som não se ouve num teste, mede-se: que nenhuma voz sai muda, que nenhuma estoira o limite dos 16 bits,
/// que o envelope morre mesmo em vez de cortar a seco, que a sirene desce de tom e que a mesma voz dá
/// exactamente os mesmos bytes duas vezes seguidas — o ruído do tambor tem gerador próprio precisamente para
/// o jogo soar igual em todas as máquinas.</summary>
public class ToneTests
{
    private static short[] Read(byte[] pcm)
    {
        var a = new short[pcm.Length / 2];
        for (int i = 0; i < a.Length; i++) a[i] = Tone.Sample(pcm, i);
        return a;
    }

    private static float Peak(short[] a, int from, int to)
    {
        float p = 0f;
        for (int i = from; i < to; i++) p = MathF.Max(p, MathF.Abs(a[i]));
        return p;
    }

    /// <summary>Quantas vezes a onda cruza o zero num troço — é a medida barata da altura do som.</summary>
    private static int Crossings(short[] a, int from, int to)
    {
        int n = 0;
        for (int i = from + 1; i < to; i++) if ((a[i - 1] < 0) != (a[i] < 0)) n++;
        return n;
    }

    [Fact]
    public void EveryVoiceHasAName_AndTheBankCoversTheSevenTimbres()
    {
        Assert.Equal(7, Tone.Bank.Count);
        foreach (var (id, v) in Tone.Bank)
        {
            Assert.Equal(id, v.Id);
            Assert.NotEmpty(v.Notes);
            Assert.NotEmpty(v.Harmonics);
            Assert.Equal(1f, v.Harmonics[0]);                 // o fundamental é sempre o que manda
            Assert.InRange(v.Each, 0.05f, 1f);                // um aviso não é uma canção
            Assert.InRange(v.Db, -20f, 0f);                   // e não rebenta o barramento
            Assert.InRange(Tone.Seconds(v), 0.05f, 2f);
        }
    }

    [Fact]
    public void TheWaveHasTheLengthTheRecipeAsksForAndFitsInSixteenBits()
    {
        foreach (var v in Tone.Bank.Values)
        {
            var pcm = Tone.Pcm16(v);
            Assert.Equal(Tone.Samples(v) * 2, pcm.Length);    // 16 bits mono: dois bytes por amostra
            var a = Read(pcm);
            float peak = Peak(a, 0, a.Length);
            Assert.True(peak > 4000f, $"{v.Id} sai quase mudo (pico {peak})");
            Assert.True(peak <= 22001f, $"{v.Id} estoira o limite (pico {peak})");
        }
    }

    [Fact]
    public void EveryVoiceDiesAwayInsteadOfBeingCutOff()
    {
        foreach (var v in Tone.Bank.Values)
        {
            var a = Read(Tone.Pcm16(v));
            int n = a.Length / v.Notes.Length;                 // mede-se dentro da primeira nota
            float head = Peak(a, 0, n / 4);
            float tail = Peak(a, n - n / 4, n);
            Assert.True(tail < head * 0.75f, $"{v.Id} não desvanece ({head} → {tail})");
        }
    }

    [Fact]
    public void TheSirenSlidesDownAndTheBellStaysWhereItIs()
    {
        var siren = Read(Tone.Pcm16(Tone.Bank["sirene"]));
        int n = siren.Length / Tone.Bank["sirene"].Notes.Length;
        // a sirene tem bend negativo: no fim da nota cruza o zero menos vezes do que no princípio
        Assert.True(Crossings(siren, 0, n / 3) > Crossings(siren, n - n / 3, n),
                    "a sirene tinha de descer de tom");

        var bell = Read(Tone.Pcm16(Tone.Bank["campainha"]));
        int b = bell.Length;
        int first = Crossings(bell, 0, b / 3), last = Crossings(bell, b - b / 3, b);
        Assert.True(MathF.Abs(first - last) < first * 0.25f, $"a campainha andou de tom ({first} → {last})");
    }

    [Fact]
    public void TheDrumIsNoiseAndTheChimeIsNot()
    {
        var drum = Tone.Bank["tambor"];
        Assert.True(drum.Noise > 0f);
        Assert.All(Tone.Bank.Values.Where(v => v.Id != "tambor"), v => Assert.Equal(0f, v.Noise));
        // com ruído por cima o sinal cruza o zero muito mais vezes do que o seno grave sozinho
        var withNoise = Read(Tone.Pcm16(drum));
        var clean = Read(Tone.Pcm16(drum with { Noise = 0f }));
        Assert.True(Crossings(withNoise, 0, withNoise.Length) > Crossings(clean, 0, clean.Length) * 2,
                    "o tambor sem ruído devia ser outra coisa");
    }

    [Fact]
    public void TheSameVoiceSoundsTheSameOnEveryMachine()
    {
        // o ruído do tambor sai de um gerador próprio semeado sempre igual: duas passagens, os mesmos bytes
        foreach (var v in Tone.Bank.Values) Assert.Equal(Tone.Pcm16(v), Tone.Pcm16(v));
    }

    [Fact]
    public void TheAlarmsAreLouderThanTheEverydayBlip()
    {
        float blip = Tone.Bank["toque"].Db;
        Assert.True(Tone.Bank["sirene"].Db > blip, "a sirene tem de sobressair");
        Assert.True(Tone.Bank["tambor"].Db > blip, "o tambor tem de sobressair");
        Assert.All(Tone.Bank.Values, v => Assert.True(v.Db >= blip, $"{v.Id} é mais discreto do que o toque"));
    }

    [Fact]
    public void ANoteThatChangesPitchChangesTheWaveAndNothingElseDoes()
    {
        var v = Tone.Bank["sino"];
        Assert.NotEqual(Tone.Pcm16(v), Tone.Pcm16(v with { Notes = new[] { 440f, 880f } }));
        Assert.Equal(Tone.Pcm16(v).Length, Tone.Pcm16(v with { Notes = new[] { 440f, 880f } }).Length);
    }
}
