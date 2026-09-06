using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma linha da folha de comparação: o mesmo número dos dois lados, quem está à frente, e se o
/// lado direito é segredo (sem espionagem não se conhecem os quartéis alheios).</summary>
/// <param name="Group">Secção da folha ("Terra", "Economia", "Guerra").</param>
/// <param name="Winner">1 = esquerda à frente, 2 = direita, 0 = empate ou incomparável.</param>
public readonly record struct CompareRow(string Group, string Label, string Left, string Right, int Winner, bool Secret);

/// <summary>Folha de comparação directa entre dois países — a pergunta que o jogador faz antes de declarar
/// guerra e que o jogo não respondia: "eu contra ele, como é que estamos?". Havia a tabela mundial, que
/// ordena toda a gente por uma nota só, e a ficha de cada país à parte; ninguém as punha lado a lado, e
/// comparar obrigava a abrir dois painéis e a fazer contas de cabeça.
///
/// O que se sabe do outro lado respeita o nevoeiro (Vision): a terra e a gente são públicas — vêem-se no
/// mapa e nos censos —, mas os quartéis, o cofre e as ogivas só com rede de informações montada, aliança,
/// ou o país a ser nosso. Sem isso a linha vem marcada como segredo e a UI põe um ponto de interrogação:
/// é melhor não saber do que saber de graça o que custa espionagem.
///
/// Estado derivado: não guarda nada, não publica eventos, não entra no save e não é ISystem.</summary>
public static class Compare
{
    /// <summary>A folha entre dois países, vista por `viewerId` (0 = observador omnisciente, para testes e
    /// para o mapa aberto).</summary>
    public static List<CompareRow> Sheet(World w, int viewerId, int leftId, int rightId)
    {
        var rows = new List<CompareRow>();
        if (!w.Countries.TryGetValue(leftId, out var a) || !w.Countries.TryGetValue(rightId, out var b)) return rows;

        bool openLeft = Knows(w, viewerId, leftId), openRight = Knows(w, viewerId, rightId);

        void Row(string group, string label, double left, double right, string fmt = "0", bool secret = false,
                 bool moreIsBetter = true)
        {
            bool hidden = secret && !(openLeft && openRight);
            int winner = hidden || Math.Abs(left - right) < 1e-6 ? 0
                       : (left > right) == moreIsBetter ? 1 : 2;
            rows.Add(new CompareRow(group, label,
                                    secret && !openLeft ? "?" : left.ToString(fmt),
                                    secret && !openRight ? "?" : right.ToString(fmt),
                                    winner, hidden));
        }

        // Terra e gente: público. Um censo não é segredo militar e vê-se no mapa de quem manda onde.
        var la = Land(w, leftId); var lb = Land(w, rightId);
        Row("Terra", "Regiões", la.Regions, lb.Regions);
        Row("Terra", "População", la.Pop, lb.Pop, "0.0");
        Row("Terra", "Fortificações", la.Forts, lb.Forts);
        Row("Terra", "Portos", la.Ports, lb.Ports);

        // Economia: o cofre é segredo, a indústria é doutrina conhecida.
        Row("Economia", "Cofre", a.Money, b.Money, "0", secret: true);
        Row("Economia", "Rendimento/dia", EconomySystem.Income(w, leftId), EconomySystem.Income(w, rightId), "0.0", secret: true);
        Row("Economia", "Indústria", a.Stat("industry"), b.Stat("industry"), "0.00");
        Row("Economia", "Estabilidade", a.Stability, b.Stability, "0");
        Row("Economia", "Tecnologias", a.Techs.Count, b.Techs.Count);

        // Guerra: o que se conta nos quartéis só se sabe com espionagem.
        var wa = War(w, leftId); var wb = War(w, rightId);
        Row("Guerra", "Divisões", wa.Divisions, wb.Divisions, "0", secret: true);
        Row("Guerra", "Homens em armas", wa.Men, wb.Men, "0.0", secret: true);
        Row("Guerra", "Reserva de recrutas", Math.Max(0f, a.Manpower), Math.Max(0f, b.Manpower), "0.0", secret: true);
        Row("Guerra", "Organização média", wa.Org, wb.Org, "0", secret: true);
        Row("Guerra", "Esquadrões aéreos", a.AirPower, b.AirPower, "0", secret: true);
        Row("Guerra", "Ogivas", a.Nukes, b.Nukes, "0", secret: true);
        Row("Guerra", "Desgaste de guerra", a.WarExhaustion, b.WarExhaustion, "0", moreIsBetter: false);

        // Nota de potência: pública, é o que o mundo inteiro comenta.
        var rank = PowerIndex.Rankings(w).ToDictionary(s => s.CountryId, s => s);
        Row("Terra", "Nota de potência", rank.TryGetValue(leftId, out var sa) ? sa.Score : 0f,
                                          rank.TryGetValue(rightId, out var sb) ? sb.Score : 0f, "0.0");
        return rows;
    }

    /// <summary>Veredicto de uma folha: quantas linhas cada lado ganha e uma frase para o topo do painel.</summary>
    public static (int Left, int Right, string Verdict) Tally(List<CompareRow> rows, string leftName, string rightName)
    {
        int l = rows.Count(r => r.Winner == 1), r2 = rows.Count(r => r.Winner == 2);
        string verdict = l == r2 ? "Estão a par: a guerra decide-se no terreno, não na folha"
                       : l > r2 ? $"{leftName} leva vantagem em {l} de {l + r2} contas"
                                : $"{rightName} leva vantagem em {r2} de {l + r2} contas";
        return (l, r2, verdict);
    }

    /// <summary>Conhecemos os quartéis deste país? O nosso, o de um aliado, ou um com rede de informações
    /// montada — a mesma porta que abre o nevoeiro no mapa.</summary>
    private static bool Knows(World w, int viewerId, int countryId) =>
        viewerId <= 0 || viewerId == countryId || !Vision.Enabled(w)
        || w.SameFaction(viewerId, countryId) || w.HasIntel(viewerId, countryId);

    private static (int Regions, double Pop, int Forts, int Ports) Land(World w, int id)
    {
        int regions = 0, forts = 0, ports = 0; double pop = 0;
        foreach (var r in w.Regions.Values)
        {
            if (r.ControllerId != id) continue;
            regions++; pop += r.Population; forts += r.Fort;
            if (r.Coastal && r.Buildings.ContainsKey("port")) ports++;
        }
        return (regions, pop / 1e6d, forts, ports);
    }

    private static (int Divisions, double Men, double Org) War(World w, int id)
    {
        int n = 0; double men = 0, org = 0;
        foreach (var d in w.Divisions.Values)
        {
            if (d.CountryId != id) continue;
            n++; org += d.Org;
            men += w.TemplateCost(d.TemplateId) * w.Rule("manpower_per_cost", 500f) * d.Hp / 100f;
        }
        return (n, men / 1e6d, n > 0 ? org / n : 0d);
    }
}
