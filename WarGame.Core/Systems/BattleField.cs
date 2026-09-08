using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma condição do campo onde a batalha se trava: a chapa que a mostra, o número como se lê, o
/// nome e a frase que diz a quem ela pesa.</summary>
public readonly record struct FieldPart(string Glyph, string Value, string Name, string Note);

/// <summary>O campo onde se combate — o que é igual para os dois lados: o chão, o rio, o forte, a largura da
/// frente e o tempo que faz. É a barra de condições que o HoI4 põe por cima do combate, e que aqui era uma
/// linha de texto corrido no subtítulo do ecrã de batalha: "Montanha · 3 dias · frente de 3 por lado ·
/// organização 180 contra 140 · 🏰 forte 2 · 🌊 rio pelo meio".
///
/// O balanço de cada lado (CombatSystem.Explain) diz porque é que aquela tropa se bate como se bate; isto
/// diz onde ela se está a bater, que é a pergunta anterior — e a única que se pode responder antes de lá
/// chegar.
///
/// Como o resto da família (GroundSystem, RegionState, BuildPlan), não faz conta nova: o chão é o
/// GroundSystem.Terrain, o forte é o GroundSystem.FortDefence, a frente é o Frontage.Width e o desgaste do
/// tempo é o WeatherSystem.BiteOn. O rio diz-se pela diferença entre o mesmo chão com e sem rio.</summary>
public static class BattleField
{
    /// <summary>Quanto é que o rio custa a quem assalta, neste chão: o mesmo terreno com rio a dividir pelo
    /// mesmo terreno sem ele. 1,00 quando a tabela de modificadores não diz nada sobre rios.</summary>
    public static float RiverBite(World w, Region r) =>
        !r.River ? 1f
        : GroundSystem.Terrain(w, r.Terrain, true, attacking: true)
          / MathF.Max(0.01f, GroundSystem.Terrain(w, r.Terrain, false, attacking: true));

    /// <summary>As condições do campo, da mais permanente para a mais passageira: chão, rio, forte, largura
    /// da frente e tempo. O chão e a frente estão sempre lá — não há batalha sem chão nem sem frente.</summary>
    public static List<FieldPart> Parts(World w, Region r)
    {
        float att = GroundSystem.Terrain(w, r, attacking: true);
        float def = GroundSystem.Terrain(w, r, attacking: false);
        string terreno = w.TerrainDefs.TryGetValue(r.Terrain, out var td) ? td.Name : r.Terrain;
        var parts = new List<FieldPart>
        {
            new(td?.Glyph ?? "roda", $"×{att:0.00}", terreno.ToLowerInvariant(),
                $"O que este chão vale a quem se bate aqui, seja de que país for:\n"
              + $"· a quem assalta ×{att:0.00}\n· a quem espera ×{def:0.00}\n"
              + "Os espíritos e a tecnologia de cada país entram por cima, no balanço de cada lado."),
        };

        if (r.River)
            parts.Add(new FieldPart("onda", $"×{RiverBite(w, r):0.00}", "rio",
                $"Atravessar o rio custa a quem assalta: ×{RiverBite(w, r):0.00} da força que teria no mesmo"
              + $" chão sem rio.\nE aperta a passagem: a frente perde {w.Rule("front_width_river", 1f):0} lugar"
              + (w.Rule("front_width_river", 1f) == 1f ? "" : "es") + "."));

        if (r.Fort > 0)
            parts.Add(new FieldPart("escudo", $"×{GroundSystem.FortDefence(w, r):0.00}", $"forte {r.Fort}",
                $"O forte só paga a quem defende: ×{GroundSystem.FortDefence(w, r):0.00}.\n"
              + $"Cada nível vale +{w.Rule("fort_defense_per_level", 0.15f):P0}, e levanta o tecto do que ali se"
              + $" pode cavar em +{w.Rule("entrench_per_fort", 1f):0.#} degrau de trincheira.\n"
              + "Tomar a região parte um nível."));

        int width = Frontage.Width(w, r);
        parts.Add(new FieldPart("coluna", $"{width}", "frente",
            $"Cabem {width} divisões de cada lado a bater ao mesmo tempo; as outras esperam em reserva e"
          + " entram quando a linha se partir.\nÉ o terreno que decide a largura"
          + (r.River ? ", e o rio aperta-a mais" : "") + " — empilhar tropa a mais não faz bater mais."));

        if (w.Season is SeasonDef s)
        {
            float bite = WeatherSystem.BiteOn(w, r.Terrain);
            parts.Add(new FieldPart(s.Glyph.Length > 0 ? s.Glyph : "floco",
                bite <= 0f ? "—" : $"−{bite:0.0}", s.Name.ToLowerInvariant(),
                bite <= 0f
                    ? $"{s.Name}: este chão não castiga ninguém agora.\nMarcha ×{s.MoveMult:0.00}."
                    : $"{s.Name}: −{bite:0.0} de organização por dia a quem está em campo aqui"
                    + $" (−{bite * w.Rule("season_shelter", 0.4f):0.0} em terreno nosso).\n"
                    + $"Marcha ×{s.MoveMult:0.00} · recomposição ×{s.OrgMult:0.00}."));
        }

        // e o céu de hoje, que é a condição mais passageira de todas: a estação dura três meses, isto dura
        // dias — quem espera pelo fim da tempestade assalta com a força toda
        if (Weather.Of(w, r) is WeatherDef sky)
        {
            float bite = GroundSystem.Terrain(w, r.Terrain, r.River, attacking: true, sky.Id)
                         / MathF.Max(0.01f, GroundSystem.Terrain(w, r.Terrain, r.River, attacking: true));
            parts.Add(new FieldPart(sky.Glyph.Length > 0 ? sky.Glyph : "chuva",
                MathF.Abs(bite - 1f) < 0.005f ? "—" : $"×{bite:0.00}", sky.Name.ToLowerInvariant(),
                RegionState.WeatherNote(w, r, sky)));
        }
        return parts;
    }

    /// <summary>As condições numa linha, para quem só quer a frase: usa-se no rodapé do mapa e no --smoke.</summary>
    public static string Line(World w, Region r) =>
        string.Join(" · ", Parts(w, r).Select(p => $"{p.Name} {p.Value}"));
}
