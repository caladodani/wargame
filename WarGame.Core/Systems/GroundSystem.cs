using WarGame.Core.Model;
using WarGame.Core.Stats;

namespace WarGame.Core.Systems;

/// <summary>Uma linha da ficha do chão: quanto vale, neste terreno, atacar e defender com uma dada tropa.
/// `Who` é quem a linha descreve — "qualquer tropa" para o chão em si, o nome do modelo para as tropas do
/// país a que o terreno faz outra coisa (blindados na montanha, infantaria no deserto).</summary>
public readonly record struct GroundLine(string Who, float Attack, float Defend);

/// <summary>O que este chão dá e tira a quem lá combate e a quem lá marcha.
///
/// O HoI4 põe isto na ficha da província: a montanha tira metade da força a quem assalta e paga um terço a
/// quem espera, o rio castiga quem o atravessa, a estrada decide quantos dias leva a coluna. No WarGame os
/// números existiam todos — na tabela `modifier` e no `move_cost` das regras — e não se viam em sítio
/// nenhum: o painel da região dizia "Montanha" e mais nada, e o jogador só descobria o preço do assalto
/// depois de o pagar.
///
/// Não há aqui conta nova nenhuma: `Fight` é, linha por linha, o `terrainAir` do CombatSystem.SideStrength,
/// e `MarchDays` é o MovementSystem.HopDays. É de propósito — uma ficha que fizesse a sua própria versão da
/// conta acabaria a mentir no dia em que alguém mexesse na outra.</summary>
public static class GroundSystem
{
    /// <summary>O que o chão (e o rio, e as tecnologias do país) fazem à força de quem aqui se bate. É a
    /// mesma parcela que o combate chama `terreno, rio e tecnologia`: as duas chamadas ao ModifierEngine,
    /// pela mesma ordem, com o mesmo chão mínimo de 0,1.</summary>
    public static float Fight(World w, Region r, int countryId, bool attacking, StatBlock stats)
    {
        var ctx = CombatSystem.BuildContext(w, r, countryId);
        var (f1, m1) = w.Modifiers.Evaluate("str", stats, ctx);
        var (f2, m2) = w.Modifiers.Evaluate(attacking ? "str_attacker" : "str_defender", stats, ctx);
        return MathF.Max(0.1f, m1 * m2 + f1 + f2);
    }

    /// <summary>Só o chão: o terreno, o rio e o céu que hoje lá está, sem os espíritos nem a tecnologia de
    /// país nenhum. É o número que responde a «quanto custa assaltar uma montanha», e o único que é igual
    /// para toda a gente.</summary>
    public static float Terrain(World w, Region r, bool attacking) =>
        Terrain(w, r.Terrain, r.River, attacking, Weather.Id(w, r));

    /// <summary>O mesmo número para um chão qualquer, dito à mão. Serve para perguntar o que muda quando se
    /// tira o rio ao terreno, ou o que a chuva lhe acrescenta — a única maneira honesta de dizer quanto é
    /// que cada um custa, sem inventar uma segunda conta ao lado desta.</summary>
    public static float Terrain(World w, string terrain, bool river, bool attacking, string weather = "")
    {
        var ctx = new ModContext().With("terrain", terrain);
        if (river) ctx["river"] = "true";
        if (weather.Length > 0) ctx["weather"] = weather;
        var stats = new StatBlock();
        var (f1, m1) = w.Modifiers.Evaluate("str", stats, ctx);
        var (f2, m2) = w.Modifiers.Evaluate(attacking ? "str_attacker" : "str_defender", stats, ctx);
        return MathF.Max(0.1f, m1 * m2 + f1 + f2);
    }

    /// <summary>A ficha, de cima para baixo: primeiro só o terreno (o que este chão custa a quem quer que
    /// seja), depois a tropa do país com os seus espíritos e tecnologias por cima, e por fim só os modelos
    /// a quem este chão faz outra coisa (uma coluna blindada na montanha não paga o que paga a infantaria).
    /// Linhas que repetissem o número da linha de cima não entram — repetir não explica nada.</summary>
    public static List<GroundLine> Explain(World w, Region r, int countryId, int max = 3)
    {
        var lines = new List<GroundLine>();
        lines.Add(new GroundLine("só o terreno", Terrain(w, r, true), Terrain(w, r, false)));
        if (!w.Countries.ContainsKey(countryId)) return lines;
        var plain = new StatBlock();
        float baseAtt = Fight(w, r, countryId, attacking: true, plain);
        float baseDef = Fight(w, r, countryId, attacking: false, plain);
        string who = "a tropa de " + w.Countries[countryId].Tag;
        if (MathF.Abs(baseAtt - lines[0].Attack) + MathF.Abs(baseDef - lines[0].Defend) >= 0.005f)
            lines.Add(new GroundLine(who, baseAtt, baseDef));

        var odd = new List<(GroundLine Line, float Gap)>();
        foreach (var t in w.Units.GetTemplates(countryId))
        {
            StatBlock st;
            try { st = w.Stats.Get(t.Id); } catch { continue; }
            float a = Fight(w, r, countryId, attacking: true, st);
            float d = Fight(w, r, countryId, attacking: false, st);
            float gap = MathF.Abs(a - baseAtt) + MathF.Abs(d - baseDef);
            if (gap >= 0.005f) odd.Add((new GroundLine(t.Name, a, d), gap));
        }
        foreach (var (line, _) in odd.OrderByDescending(x => x.Gap).Take(Math.Max(0, max)))
            lines.Add(line);
        return lines;
    }

    /// <summary>O que a fortificação paga a quem defende aqui — a mesma conta que o CombatSystem aplica à
    /// linha de defesa antes de trocar golpes.</summary>
    public static float FortDefence(World w, Region r) => 1f + r.Fort * w.Rule("fort_defense_per_level", 0.15f);

    /// <summary>Dias que esta divisão leva a entrar neste chão, com a estrada, a estação e o comando que
    /// tem hoje. A origem só conta em travessia marítima, por isso a própria região serve de origem: o que
    /// se quer aqui é o preço do destino.</summary>
    public static float MarchDays(World w, Region target, Division d) => MovementSystem.HopDays(w, d, target, target);
}
