using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma obra que se pode mandar fazer: o que é, a chapa que a mostra, o que custa, quanto demora e
/// o que dá. Os edifícios vêm da tabela `building`; a estrada e o forte são regras (infra_*, fort_*) e não
/// linhas, por isso entram aqui com os ids reservados <see cref="BuildPlan.Infra"/> e
/// <see cref="BuildPlan.Fort"/> — os mesmos que o menu Construir já usava.</summary>
public readonly record struct BuildOffer(string Id, string Name, string Glyph, float Cost, float Days, string Gives);

/// <summary>Uma parcela da conta de uma obra, como as da produção e as da região.</summary>
public readonly record struct BuildPart(string Glyph, string Value, string Name, string Note);

/// <summary>Porque é que a obra custa o que custa, demora o que demora e dá o que dá — e porque é que às
/// vezes não se pode mandar fazer.
///
/// O menu Construir dizia "Arsenal (120, 60 d)" e mais nada: nem o que o Arsenal faz ao país, nem que só há
/// duas fábricas civis e ambas estão ocupadas, nem que aquela região já tem o nível máximo. No HoI4 o menu
/// de construção diz o efeito de cada obra e apaga o que não se pode construir, com a razão à vista.
///
/// A regra da casa mantém-se: aqui não se re-decide nada. O "não podes" é o do próprio comando
/// (BuildBuildingCommand, BuildInfrastructureCommand, BuildFortCommand), pedido a eles — se um dia a regra
/// mudar num, muda no menu no mesmo dia.</summary>
public static class BuildPlan
{
    /// <summary>Ids reservados das obras que não são linhas da tabela `building`.</summary>
    public const string Infra = "@infra", Fort = "@fort", Rail = "@rail";

    /// <summary>Tudo o que se pode mandar construir: as linhas da tabela e, no fim, a estrada e o forte.</summary>
    public static List<BuildOffer> Offers(World w)
    {
        var list = w.BuildingDefs.Values.OrderBy(d => d.Id)
            .Select(d => new BuildOffer(d.Id, d.Name, d.Glyph, d.Cost, d.Days, Gives(w, d)))
            .ToList();
        list.Add(new BuildOffer(Infra, "Infra-estrutura", "estrada",
            w.Rule("infra_build_cost", 40f), w.Rule("infra_build_days", 30f),
            $"+{w.Rule("infra_step", 0.25f):0.00} de estrada na região (tecto ×{w.Rule("infra_max", 2f):0.00}):"
          + " mais rendimento, mais abastecimento e marcha mais rápida"));
        list.Add(new BuildOffer(Fort, "Fortificação", "escudo",
            w.Rule("fort_build_cost", 30f), w.Rule("fort_build_days", 20f),
            $"+1 nível de forte (tecto {(int)w.Rule("fort_max", 5f)}): cada nível dá"
          + $" ×{1f + w.Rule("fort_defense_per_level", 0.15f):0.00} a quem defende esta região"));
        list.Add(new BuildOffer(Rail, "Carril", "carril",
            w.Rule("rail_cost", 25f), w.Rule("rail_days", 20f),
            $"+1 nível de via férrea (tecto {(int)w.Rule("rail_max", 4f)}): cada nível conta como"
          + $" +{w.Rule("rail_step", 0.5f):0.00} de estrada só para a rede de abastecimento — o salto da rede"
          + " nesta região passa a custar menos e o mapa desenha a linha"));
        return list;
    }

    public static BuildOffer? Find(World w, string id) => Offers(w).FirstOrDefault(o => o.Id == id) is { Id: not null } o ? o : null;

    /// <summary>O que este edifício faz ao país, em palavras: o multiplicador por nível, a fila de fábricas
    /// que abre e o abastecimento que projecta por mar. Tudo da tabela — nada disto está escrito em C#.</summary>
    public static string Gives(World w, BuildingDef d)
    {
        var bits = new List<string>();
        if (d.StatKey.Length > 0 && MathF.Abs(d.PerLevel) > 1e-4f)
            bits.Add($"+{d.PerLevel:P0} de {d.StatKey} por nível");
        if (d.Yard.Length > 0)
            bits.Add(d.Yard switch
            {
                "civil" => "abre uma fábrica civil por nível (obras em paralelo)",
                "militar" => "abre uma linha de montagem por nível (produção)",
                "naval" => "abre um estaleiro por nível (abastecimento pelo mar)",
                _ => $"abre uma fila de {d.Yard} por nível",
            });
        if (d.SupplyRange > 0f) bits.Add($"leva abastecimento a {d.SupplyRange:0} km por nível");
        if (d.IsHub) bits.Add($"nasce aqui uma cabeça de rede que abastece {d.HubRange:0.#} saltos à volta por nível"
                            + " — e é a única obra que se levanta em terra tomada ao inimigo");
        if (d.Coastal) bits.Add("só se constrói em região de costa");
        bits.Add($"até ao nível {d.MaxLevel}");
        return string.Join("; ", bits);
    }

    /// <summary>O que impede esta obra nesta região, palavra por palavra do comando que a manda fazer.
    /// Null = pode-se. Sem região (o menu ainda não sabe onde se vai tocar) responde só ao que é de país:
    /// o cofre e as fábricas civis.</summary>
    public static string? Blocked(World w, int countryId, int? regionId, string id)
    {
        if (regionId is int rid)
            return (id == Infra ? new BuildInfrastructureCommand(countryId, rid).Validate(w)
                  : id == Fort ? new BuildFortCommand(countryId, rid).Validate(w)
                  : id == Rail ? new BuildRailCommand(countryId, rid).Validate(w)
                  : new BuildBuildingCommand(countryId, rid, id).Validate(w));

        if (Find(w, id) is not BuildOffer offer) return "obra desconhecida";
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Capitulated) return "país inválido";
        if (c.Money < offer.Cost) return $"faltam pontos de produção ({offer.Cost:0})";
        if (Industry.Of(w, countryId).FreeCivil <= 0) return "fábricas civis todas ocupadas";
        return null;
    }

    /// <summary>As parcelas da obra: o que custa, quanto demora, quantas fábricas civis sobram e em que pé
    /// está a região (se já se sabe qual é).</summary>
    public static List<BuildPart> Parts(World w, int countryId, int? regionId, string id)
    {
        var parts = new List<BuildPart>();
        if (Find(w, id) is not BuildOffer offer) return parts;
        var yards = Industry.Of(w, countryId);
        float money = w.Countries.TryGetValue(countryId, out var c) ? c.Money : 0f;

        parts.Add(new BuildPart("cofre", $"{offer.Cost:0}", "custo",
            $"Paga-se todo no dia em que a obra começa.\nNo cofre: {money:0.0}"
          + (money < offer.Cost ? "\nNão chega." : "")));
        parts.Add(new BuildPart("sol", $"{offer.Days:0}", "dias",
            "Uma obra anda um dia por dia — as fábricas civis dizem quantas obras andam ao mesmo tempo,"
          + " não a que velocidade cada uma anda."));
        parts.Add(new BuildPart("fabrica", $"{yards.FreeCivil}/{yards.Civil}", "fábricas civis",
            "Cada obra em curso ocupa uma fábrica civil até acabar. Sem nenhuma livre não se começa"
          + " mais nenhuma, por muito dinheiro que haja no cofre."));
        parts.Add(new BuildPart(offer.Glyph, Level(w, regionId, offer), "nível", offer.Gives));
        return parts;
    }

    /// <summary>Em que pé está esta obra na região escolhida: o nível que lá está e o tecto. Sem região, só
    /// o tecto — é o que se sabe antes de o dedo tocar no mapa.</summary>
    private static string Level(World w, int? regionId, BuildOffer offer)
    {
        if (regionId is not int rid || !w.Regions.TryGetValue(rid, out var r))
            return offer.Id == Infra ? $"até ×{w.Rule("infra_max", 2f):0.00}"
                 : offer.Id == Fort ? $"até {(int)w.Rule("fort_max", 5f)}"
                 : offer.Id == Rail ? $"até {(int)w.Rule("rail_max", 4f)}"
                 : w.BuildingDefs.TryGetValue(offer.Id, out var d0) ? $"até {d0.MaxLevel}" : "—";
        if (offer.Id == Infra) return $"×{r.Infrastructure:0.00} de ×{w.Rule("infra_max", 2f):0.00}";
        if (offer.Id == Fort) return $"{r.Fort} de {(int)w.Rule("fort_max", 5f)}";
        if (offer.Id == Rail) return $"{Math.Max(0, r.Rail)} de {(int)w.Rule("rail_max", 4f)}";
        return w.BuildingDefs.TryGetValue(offer.Id, out var d)
            ? $"{r.Buildings.GetValueOrDefault(offer.Id)} de {d.MaxLevel}" : "—";
    }

    /// <summary>A mesma conta em texto corrido, para o tooltip de um botão do menu.</summary>
    public static string Why(World w, int countryId, int? regionId, string id)
    {
        if (Find(w, id) is not BuildOffer offer) return "";
        string travao = Blocked(w, countryId, regionId, id) is string b ? $"\nnão se pode agora: {b}" : "";
        return $"{offer.Name}\n{offer.Gives}\n"
             + string.Join("\n", Parts(w, countryId, regionId, id).Select(p => $"· {p.Name}: {p.Value}"))
             + travao;
    }
}
