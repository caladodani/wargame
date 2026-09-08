using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma parcela da ficha da nação: a chapa que a mostra, o número como se lê, o nome e a frase que
/// diz de onde ele vem.</summary>
public readonly record struct NationPart(string Glyph, string Value, string Name, string Note);

/// <summary>A ficha de como o PAÍS está — o que o painel da região já tinha (RegionState) e o painel do país
/// ainda não: as três filas de fábricas, o cofre, os homens, a estabilidade, o exército, a terra, os
/// laboratórios e o lugar na tabela mundial.
///
/// O painel dizia isto tudo em quatro parágrafos corridos — "Indústria ×1,00   Produção ×1,00   Organização
/// ×1,00   Investigação ×1,00" seguidos de "Divisões 12 · Regiões 40 · Rendimento 8,4/dia" — que se liam de
/// uma ponta à outra para encontrar um número e não diziam de onde ele vinha. No HoI4 esta é a barra de cima
/// do ecrã inteiro: fábricas civis, fábricas militares, estaleiros, homens, estabilidade, e cada uma com a
/// sua conta em cima do dedo.
///
/// Como no RegionState, no RegionYield e no BuildPlan, aqui não se faz conta nova nenhuma: as fábricas são
/// as do Industry, o rendimento é o do EconomySystem, o tecto e o ganho de homens são os do ManpowerSystem,
/// o alvo da estabilidade é o do StabilitySystem, as ranhuras são as do ResearchSystem e os recursos são os
/// do ResourceSystem.</summary>
public static class NationSheet
{
    /// <summary>A ficha, parcela a parcela. As fábricas, o cofre, os homens, a estabilidade, o exército, a
    /// terra e os laboratórios estão sempre lá — um país sem divisões é informação. O lugar no mundo e os
    /// estaleiros só aparecem quando existem.
    ///
    /// O <paramref name="statName"/> traduz a chave de característica para o nome que o jogador lê; sem ele
    /// a ficha diz a chave, que é o que serve aos testes.</summary>
    public static List<NationPart> Parts(World w, Country c, Func<string, string>? statName = null)
    {
        statName ??= k => k;
        var yards = Industry.Of(w, c.Id);
        float pop = ManpowerSystem.Pop(w, c.Id);
        var parts = new List<NationPart>();

        if (c.PowerRank > 0)
        {
            string move = c.PowerRankPrev > 0 && c.PowerRankPrev != c.PowerRank
                ? c.PowerRank < c.PowerRankPrev ? $"\nSubiu {c.PowerRankPrev - c.PowerRank} lugar(es) desde a última contagem."
                                                : $"\nDesceu {c.PowerRank - c.PowerRankPrev} lugar(es) desde a última contagem."
                : "";
            parts.Add(new NationPart("globo", $"{c.PowerRank}.º", "no mundo",
                $"Nota de potência {c.PowerScore:0.0}, de gente, indústria, exército e ciência."
              + $"\nA tabela refaz-se de {(int)w.Rule("power_rank_days", 30f)} em {(int)w.Rule("power_rank_days", 30f)} dias." + move));
        }

        parts.Add(new NationPart("fabrica", $"{yards.FreeCivil} de {yards.Civil}", "fábricas civis",
            "Cada obra — estrada, forte ou edifício — ocupa uma enquanto dura."
          + "\nSem nenhuma livre não há obra que comece, por muito dinheiro que haja."));
        parts.Add(new NationPart("bigorna", $"{yards.MilitaryBusy} de {yards.Military}", "fábricas militares",
            "Linhas de montagem a andar hoje na fila de produção."
          + "\nCada divisão em construção ocupa uma; as que sobram não fazem nada."));
        if (yards.Naval > 0)
            parts.Add(new NationPart("ancora", $"{yards.NavalBusy} de {yards.Naval}", "estaleiros",
                $"Levam abastecimento do outro lado do mar, {w.Rule("yard_divisions", 3f):0} divisões cada."));

        parts.Add(new NationPart("cofre", $"{EconomySystem.Income(w, c.Id):0.0}/dia", "rendimento",
            $"O que as regiões controladas rendem, já com indústria ×{c.Stat("industry"):0.00}"
          + $" e estabilidade ×{c.StabilityFactor:0.00}.\nNo cofre agora: {c.Money:0}."));

        parts.Add(new NationPart("gente", People(c.Manpower < 0f ? 0f : c.Manpower), "homens",
            $"Tecto {People(ManpowerSystem.Cap(w, c, pop))}, de {People(pop)} recrutáveis."
          + $"\nEntram {ManpowerSystem.Gain(w, c, pop):0} por dia, à recruta ×{c.Stat("conscription"):0.00}."));

        float occ = StabilitySystem.OccupiedShare(w, c.Id);
        float target = StabilitySystem.Target(w, c, occ);
        parts.Add(new NationPart("balanca", $"{c.Stability:0}%", "estabilidade",
            $"Anda {w.Rule("stability_speed", 0.5f):0.0} por dia para {target:0}%."
          + (c.AtWarWith.Count > 0 ? $"\n{Math.Min(2, c.AtWarWith.Count)} guerra(s) pesam −{Math.Min(2, c.AtWarWith.Count) * w.Rule("stability_war_penalty", 10f):0}." : "")
          + (occ > 0.005f ? $"\n{occ:P0} do nosso povo está ocupado: −{occ * w.Rule("stability_occupied_penalty", 40f):0}." : "")
          + (c.WarExhaustion >= 1f ? $"\nDesgaste de guerra: −{c.WarExhaustion:0}." : "")
          + $"\nMultiplica o cofre e a recruta por ×{c.StabilityFactor:0.00}."));

        parts.Add(new NationPart("capacete", $"{w.Divisions.Values.Count(d => d.CountryId == c.Id)}", "divisões",
            $"Tropa em campo, com organização ×{c.Stat("org_regain"):0.00} a recompor-se por dia."));

        parts.Add(new NationPart("bandeira", $"{w.Regions.Values.Count(r => r.ControllerId == c.Id)}", "regiões",
            $"Terra que controlamos hoje, de {w.Regions.Values.Count(r => r.OwnerId == c.Id)} que são nossas."));

        int slots = ResearchSystem.Slots(w, c);
        parts.Add(new NationPart("livro", $"{c.Research.Count} de {slots}", "laboratórios",
            $"Linhas de investigação a andar, a {c.Stat("research_speed"):0.00} de progresso por dia cada."
          + $"\n{c.Techs.Count} tecnologia(s) já concluída(s)."));

        return parts;
    }

    /// <summary>Os recursos estratégicos que este país tem debaixo do pé, com o que cada um lhe dá. Só os que
    /// tem: uma chapa a zero por cada minério do mundo era ruído.</summary>
    public static List<NationPart> Resources(World w, Country c, Func<string, string>? statName = null)
    {
        statName ??= k => k;
        var list = new List<NationPart>();
        foreach (var d in w.ResourceDefs.Values.OrderBy(d => d.Id))
        {
            float units = ResourceSystem.Controlled(w, c.Id, d.Id);
            if (units <= 0f) continue;
            float used = MathF.Min(units, d.Cap);
            list.Add(new NationPart(d.Glyph, $"{units:0}", d.Name,
                $"+{used * d.PerUnit:P0} em {statName(d.StatKey)}, de {used:0} unidade(s) aproveitadas."
              + (units > d.Cap ? $"\nAcima de {d.Cap:0} não rende mais: as {units - d.Cap:0} que sobram vendem-se." : "")
              + (d.FuelPerUnit > 0f ? $"\nRefina {units * d.FuelPerUnit:0.0} de combustível por dia." : "")));
        }
        return list;
    }

    /// <summary>A ficha dita por palavras — a mesma que as chapas mostram, para o rodapé e para o --smoke.</summary>
    public static string Line(World w, Country c) =>
        string.Join(" · ", Parts(w, c).Select(p => $"{p.Name} {p.Value}"));

    /// <summary>Gente como se diz: milhões, milhares, ou o número quando é pouca.</summary>
    public static string People(float p) => RegionState.People(p);
}
