using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma parcela do estado de uma região: a chapa que a mostra, o número como se lê, o nome e a
/// frase que diz o que aquele número faz.</summary>
public readonly record struct StatePart(string Glyph, string Value, string Name, string Note);

/// <summary>Uma obra a decorrer na região: a chapa, o nome, os dias que faltam e a fracção já feita.</summary>
public readonly record struct WorkPart(string Glyph, string Name, int DaysLeft, float Progress, string Note);

/// <summary>Como esta região ESTÁ — a metade que faltava depois do que o chão tira (GroundSystem) e do que a
/// terra dá (RegionYield): a gente, a estrada, o forte, o cais, o tempo que lá faz, a resistência, a
/// integração, as fábricas civis que sobram, a conta do modo de mapa e as obras a andar.
///
/// Tudo isto já se lia no jogo, mas escrito num parágrafo corrido de emojis e pontos no topo do painel da
/// região — "🔧 danificada · 🏰 Forte 2 · ⚓ cais 1 · ✊ resistência 34%" — que se lia de uma ponta à outra
/// para encontrar um número. No HoI4 a ficha do estado é uma grelha de ícones com valor por baixo, e é por
/// ela que se decide o que se fortifica, o que se guarnece e o que se deixa cair.
///
/// Como no RegionYield, no ProductionPlan e no BuildPlan, aqui não se faz conta nova nenhuma: o forte é o
/// GroundSystem.FortDefence, o desgaste do tempo é o WeatherSystem.BiteOn, a integração usa os dias e a
/// velocidade do IntegrationSystem, as obras usam os dias do ConstructionSystem e as fábricas são as do
/// Industry.</summary>
public static class RegionState
{
    /// <summary>Níveis de cais de uma região: só os edifícios que alcançam mar (building.supply_range).</summary>
    public static int PortLevels(World w, Region r) =>
        r.Buildings.Where(kv => w.BuildingDefs.TryGetValue(kv.Key, out var d) && d.SupplyRange > 0f).Sum(kv => kv.Value);

    /// <summary>Alcance por mar somado dos cais de uma região, em quilómetros de travessia.</summary>
    public static float PortReach(World w, Region r) =>
        r.Buildings.Sum(kv => w.BuildingDefs.TryGetValue(kv.Key, out var d) ? d.SupplyRange * kv.Value : 0f);

    /// <summary>Quem manda nesta terra, numa linha: o controlador e, quando é ocupação, de quem ela é.</summary>
    public static string Control(World w, Region r)
    {
        string ctrl = w.Countries.TryGetValue(r.ControllerId, out var c) ? c.Name : "—";
        if (r.ControllerId == r.OwnerId) return ctrl;
        string dono = w.Countries.TryGetValue(r.OwnerId, out var o) ? o.Name : "—";
        return $"{ctrl} · ocupada, de {dono}";
    }

    /// <summary>O que a estação está a custar à tropa NESTE chão, por dia. Vazio sem estação carregada.</summary>
    public static string SeasonNote(World w, Region r)
    {
        if (w.Season is not SeasonDef s) return "";
        float bite = WeatherSystem.BiteOn(w, r.Terrain);
        string cost = bite <= 0f
            ? "sem desgaste"
            : $"−{bite:0.0} org/dia em campo (−{bite * w.Rule("season_shelter", 0.4f):0.0} em terreno nosso)";
        return $"{s.Name}: {cost} · marcha ×{s.MoveMult:0.00}";
    }

    /// <summary>A ficha do estado, parcela a parcela. A gente e a estrada estão sempre lá (uma região sem
    /// gente é informação); o resto só aparece quando existe — um forte a zero não ocupa espaço no ecrã.
    /// O <paramref name="mapMode"/> é o modo em que o mapa está pintado: com o mapa a pintar abastecimento,
    /// a ficha diz o número exacto desta região, que a cor sozinha não dá.</summary>
    public static List<StatePart> Parts(World w, Region r, int? viewerId = null, string mapMode = "")
    {
        var parts = new List<StatePart>
        {
            new("gente", People(r.Population), "habitantes",
                "Gente desta terra. Dá homens ao recrutamento e pontos ao cofre todos os dias"
              + (r.ControllerId != r.OwnerId ? $"\nTerra ocupada: só ×{OccupationSystem.ManpowerMult(w, r):0.00} dela se recruta." : ".")),
            new("estrada", $"×{r.Infrastructure:0.00}", "estrada", InfraNote(w, r)),
        };

        if (r.Fort > 0)
            parts.Add(new StatePart("escudo", $"{r.Fort}", "forte",
                $"Cada nível dá vantagem a quem defende esta região.\n"
              + $"Aos {r.Fort} de agora: ×{GroundSystem.FortDefence(w, r):0.00} à defesa.\n"
              + $"Tecto: {(int)w.Rule("fort_max", 5f)} níveis."));

        int quay = PortLevels(w, r);
        if (quay > 0)
            parts.Add(new StatePart("ancora", $"{quay}", "cais",
                $"Carrega {quay * w.Rule("port_capacity_per_level", 6f):0} divisões do outro lado do mar,"
              + $" até {PortReach(w, r):0} km de travessia.\nCais a rebentar aperta o abastecimento de quem desembarcou."));

        if (w.Season is SeasonDef s)
            parts.Add(new StatePart(s.Glyph.Length > 0 ? s.Glyph : "floco",
                s.MoveMult >= 1f ? $"+{s.MoveMult - 1f:P0}" : $"−{1f - s.MoveMult:P0}", "marcha", SeasonNote(w, r)));

        if (r.Resistance > 0.005f)
            parts.Add(new StatePart("punho", $"{r.Resistance:P0}", "resistência",
                $"Sobe {w.Rule("resistance_growth", 0.02f):P0} por dia enquanto o dono estiver vivo e não houver"
              + $" divisão nossa aqui; uma guarnição faz descer {w.Rule("resistance_suppress", 0.04f):P0} por dia.\n"
              + $"Tira {r.Resistance * w.Rule("resistance_output_hit", 0.5f):P0} do que esta terra rende.\n"
              + "A 100% sem guarnição a terra revolta-se e volta ao dono."));

        if (r.Integration > 0.5f)
        {
            float need = MathF.Max(1f, w.Rule("integration_days", 150f));
            float speed = w.Countries.TryGetValue(r.ControllerId, out var occ) ? occ.Stat("integration_speed") : 1f;
            int falta = speed > 0f ? (int)MathF.Ceiling((need - r.Integration) / speed) : -1;
            parts.Add(new StatePart("aperto", $"{r.Integration / need:P0}", "integração",
                $"Ao fim de {need:0} dias de ocupação calma a terra passa a ser nossa.\n"
              + (falta >= 0 ? $"Ao ritmo de agora (×{speed:0.00}): faltam {falta} dias.\n" : "")
              + $"Resistência acima de {w.Rule("integration_max_resist", 0.1f):P0} faz o progresso recuar"
              + $" {w.Rule("integration_decay", 2f):0} por dia."));
        }

        // fábricas civis: a obra pode ser recusada com o cofre cheio, e sem isto não se percebia porquê
        if (viewerId is int owner && r.OwnerId == owner && r.ControllerId == owner)
        {
            var yards = Industry.Of(w, owner);
            parts.Add(new StatePart("fabrica", $"{yards.FreeCivil} de {yards.Civil}", "fábricas civis",
                "Fábricas civis livres do país inteiro, não só desta terra.\nSem uma livre, nenhuma obra nova arranca."));
        }

        if (w.MapModeDefs.GetValueOrDefault(mapMode) is MapModeDef mode && mode.Metric != "owner"
            && MapModes.Text(w, viewerId ?? 0, r, mode.Metric) is string mText && mText.Length > 0)
            parts.Add(new StatePart(mode.Glyph.Length > 0 ? mode.Glyph : "globo", mText, mode.Name.ToLowerInvariant(),
                $"O mapa está pintado por {mode.Name.ToLowerInvariant()}: da cor "
              + $"{mode.Low.ToLowerInvariant()} à cor {mode.High.ToLowerInvariant()}.\nAqui vale {mText}."));

        return parts;
    }

    /// <summary>Gente como se diz: aos milhões acima do milhão, aos milhares acima do milhar. Uma vila de
    /// trinta mil lia-se "0,0 M", que é o mesmo que não dizer nada.</summary>
    public static string People(float p) =>
        p >= 1e6f ? $"{p / 1e6f:0.0} M" : p >= 1000f ? $"{p / 1000f:0} k" : $"{p:0}";

    /// <summary>A estrada desta terra: o que multiplica, e se está partida, quanto falta para se repor
    /// sozinha (a obra paga sobe mais depressa, mas custa).</summary>
    private static string InfraNote(World w, Region r)
    {
        string note = "Multiplica o que a terra rende e o que a tropa recebe de abastecimento.";
        if (r.Infrastructure < r.BaseInfrastructure - 1e-4f)
        {
            float perDay = w.Rule("infra_repair_per_day", 0.002f);
            int dias = perDay > 0f ? (int)MathF.Ceiling((r.BaseInfrastructure - r.Infrastructure) / perDay) : -1;
            note += $"\nPartida pela guerra: repõe-se sozinha até ×{r.BaseInfrastructure:0.00}"
                  + (dias >= 0 ? $" em {dias} dias" : "")
                  + $" — mas não enquanto houver batalha aqui, nem em terra ocupada com resistência acima de"
                  + $" {w.Rule("infra_repair_max_resist", 0.3f):P0}.";
        }
        return note;
    }

    /// <summary>As obras a decorrer nesta região: a estrada, o forte e o edifício. Os dias que faltam são os
    /// do ConstructionSystem — um dia de progresso por dia.</summary>
    public static List<WorkPart> Works(World w, Region r)
    {
        var works = new List<WorkPart>();
        if (r.Building)
        {
            float days = MathF.Max(1f, w.Rule("infra_build_days", 30f));
            works.Add(new WorkPart("estrada", "estrada", (int)MathF.Ceiling(days - r.BuildProgress),
                Math.Clamp(r.BuildProgress / days, 0f, 1f),
                $"Ao fim da obra a estrada sobe +{w.Rule("infra_step", 0.25f):0.00} (tecto ×{w.Rule("infra_max", 2f):0.00}).\n"
              + "Terra que caia nas mãos do inimigo perde a obra e o dinheiro."));
        }
        if (r.FortBuilding)
        {
            float days = MathF.Max(1f, w.Rule("fort_build_days", 20f));
            works.Add(new WorkPart("escudo", "forte", (int)MathF.Ceiling(days - r.FortProgress),
                Math.Clamp(r.FortProgress / days, 0f, 1f),
                $"Ao fim da obra o forte sobe para {Math.Min((int)w.Rule("fort_max", 5f), r.Fort + 1)}."));
        }
        if (r.Project is string proj && w.BuildingDefs.TryGetValue(proj, out var pd))
        {
            float days = MathF.Max(1f, pd.Days);
            works.Add(new WorkPart(pd.Glyph.Length > 0 ? pd.Glyph : "fabrica", pd.Name.ToLowerInvariant(),
                (int)MathF.Ceiling(days - r.ProjectProgress), Math.Clamp(r.ProjectProgress / days, 0f, 1f),
                $"{pd.Name} nível {Math.Min(pd.MaxLevel, r.Buildings.GetValueOrDefault(proj) + 1)} de {pd.MaxLevel}"
              + (pd.StatKey.Length > 0 ? $"\n+{pd.PerLevel:P0} de {pd.StatKey} por nível" : "")));
        }
        return works;
    }

    /// <summary>A batalha que aqui se trava, numa linha: quem ataca, há quantos dias e a organização dos dois
    /// lados (que é o que decide, não os efectivos). Null quando a terra está em paz.</summary>
    public static string? BattleLine(World w, Region r)
    {
        var b = w.ActiveBattles.FirstOrDefault(x => x.RegionId == r.Id);
        if (b is null) return null;
        float attOrg = b.Attackers.Sum(id => w.Divisions.TryGetValue(id, out var d) ? d.Org : 0f);
        float defOrg = b.Defenders.Sum(id => w.Divisions.TryGetValue(id, out var d) ? d.Org : 0f);
        string attTag = w.Countries.TryGetValue(b.AttackerCountryId, out var ac) ? ac.Tag : "?";
        return $"batalha ({b.Days} dias): {attTag} ataca — org {attOrg:0} vs {defOrg:0}"
             + (r.Fort > 0 ? $" (forte {r.Fort})" : "");
    }
}
