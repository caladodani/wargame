using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>O tempo que faz HOJE e AQUI. A estação do ano já existia, mas manda no ano inteiro e no mundo
/// inteiro de uma vez: em Janeiro atolava-se tanto no Sara como na Carélia, e o céu nunca era notícia. No
/// HoI4 é ao contrário — chove num troço da frente e não no outro, e é a chuva de amanhã que faz adiar uma
/// ofensiva de dez divisões. Faltava-nos essa camada: a semana e a região.
///
/// Nada disto se guarda. O céu é uma conta: a latitude da região com a estação por cima dá o frio, o frio
/// diz que céus são possíveis (tabela weather) e um sorteio determinista escolhe entre eles. Determinista
/// para valer a pena: o mesmo dia no mesmo mundo dá sempre o mesmo tempo, com save ou sem ele, no telefone
/// como no CI — um tempo que mudasse ao recarregar seria um tempo em que ninguém apostava.
///
/// O sorteio é por CÉLULA do mapa (weather_cell) e não por região: assim o mau tempo vem em frentes largas,
/// como se vê no mapa do HoI4, em vez de salpicar o mundo região sim região não. A célula dá o sorteio; o
/// frio de cada região dentro dela decide se aquilo cai em chuva ou em neve.
///
/// O que o céu faz: atrasa a marcha (MovementSystem), estraga a recomposição (RecoverySystem), fecha o céu à
/// aviação (AirMissionSystem) e castiga quem assalta — essa parte vive na tabela modifier, ao lado do
/// terreno e do rio, para o combate, a ficha do chão e o ecrã de batalha contarem todos a mesma história.
///
/// Não é um ISystem: não tem estado nenhum para adiantar por tick.</summary>
public static class Weather
{
    /// <summary>O céu desta região neste dia, ou null se a tabela weather não veio carregada (mundos de
    /// teste que só carregam meia base de dados continuam a andar sem tempo nenhum).</summary>
    public static WeatherDef? Of(World w, Region r)
    {
        if (w.WeatherDefs.Count == 0) return null;
        float cold = Cold(w, r);
        // os elegíveis: o frio de hoje tem de cair na faixa do céu, e o chão tem de ser o dele
        float total = 0f;
        foreach (var d in w.WeatherDefs.Values)
            if (Fits(d, r, cold)) total += MathF.Max(0f, d.Weight);
        if (total <= 0f) return null;

        float pick = Roll(w, r) * total, run = 0f;
        WeatherDef? last = null;
        foreach (var d in w.WeatherDefs.Values.OrderBy(d => d.Sort).ThenBy(d => d.Id))
        {
            if (!Fits(d, r, cold)) continue;
            last = d;
            run += MathF.Max(0f, d.Weight);
            if (pick < run) return d;
        }
        return last;   // arredondamentos: o último elegível fica com a ponta do intervalo
    }

    private static bool Fits(WeatherDef d, Region r, float cold) =>
        cold >= d.ColdMin && cold <= d.ColdMax && (d.Terrain.Length == 0 || d.Terrain == r.Terrain);

    /// <summary>O frio desta terra hoje, de 0 (trópico) a 1 (círculo polar em pleno Inverno). É a latitude
    /// que manda — nos trópicos nunca neva, ande o calendário por onde andar — e a estação que a agrava,
    /// ao contrário no hemisfério sul: em Janeiro é Verão na Argentina e ninguém lá anda na neve.</summary>
    public static float Cold(World w, Region r)
    {
        float band = Math.Clamp(MathF.Abs(r.Lat) / 66f, 0f, 1f);          // 66° = círculo polar
        float season = w.Season?.Cold ?? 0.5f;
        if (r.Lat < 0f) season = 1f - season;                             // a sul as estações andam trocadas
        float floorShare = Math.Clamp(w.Rule("weather_cold_floor", 0.35f), 0f, 1f);
        return Math.Clamp(band * (floorShare + (1f - floorShare) * season), 0f, 1f);
    }

    /// <summary>O sorteio desta célula do mapa neste bloco de dias, entre 0 e 1. Sem aleatório nenhum: uma
    /// mistura de inteiros, para o mesmo mundo dar sempre o mesmo céu.</summary>
    private static float Roll(World w, Region r)
    {
        float cell = MathF.Max(1f, w.Rule("weather_cell", 900f));
        float days = MathF.Max(1f, w.Rule("weather_days", 5f));
        int cx = (int)MathF.Floor(r.CenterX / cell), cy = (int)MathF.Floor(r.CenterY / cell);
        int block = (int)MathF.Floor(w.Clock.Day / days);
        return Hash(cx, cy, block);
    }

    /// <summary>Mistura de três inteiros num número entre 0 e 1 (variante do murmur de 32 bits). Igual em
    /// qualquer máquina: são só somas, multiplicações e deslocamentos sem sinal. Público porque as tácticas
    /// (Tactics) sorteiam da mesma maneira — dois sorteios diferentes seriam duas maneiras de sortear.</summary>
    public static float Hash(int x, int y, int z)
    {
        unchecked
        {
            uint h = (uint)x * 73_856_093u ^ (uint)y * 19_349_663u ^ (uint)z * 83_492_791u;
            h ^= h >> 16; h *= 2_246_822_519u;
            h ^= h >> 13; h *= 3_266_489_917u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / (float)0x1000000u;
        }
    }

    /// <summary>O id do céu de hoje aqui, ou "" sem tempo carregado. É o que entra no contexto do combate
    /// (condition_key 'weather' da tabela modifier).</summary>
    public static string Id(World w, Region r) => Of(w, r)?.Id ?? "";

    /// <summary>Quanto do passo normal se anda hoje nesta região (1 = céu limpo).</summary>
    public static float MoveMult(World w, Region r) => MathF.Max(0.05f, Of(w, r)?.MoveMult ?? 1f);

    /// <summary>Quanto da recomposição normal se ganha hoje nesta região (1 = céu limpo).</summary>
    public static float OrgMult(World w, Region r) => MathF.Max(0.05f, Of(w, r)?.OrgMult ?? 1f);

    /// <summary>Quanto a aviação consegue fazer hoje no céu desta região (1 = céu limpo, 0.2 num nevão).</summary>
    public static float AirMult(World w, Region r) => MathF.Max(0f, Of(w, r)?.AirMult ?? 1f);

    /// <summary>O céu numa linha, para a ficha da região e para o mapa: nome, o que atrasa e o que fecha.</summary>
    public static string Line(World w, Region r)
    {
        if (Of(w, r) is not WeatherDef d) return "";
        string march = d.MoveMult >= 1f ? "marcha à vontade" : $"marcha ×{d.MoveMult:0.00}";
        return $"{d.Name}: {d.Note} {march} · recomposição ×{d.OrgMult:0.00} · aviação ×{d.AirMult:0.00}";
    }
}
