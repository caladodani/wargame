using WarGame.Core.Model;
using WarGame.Core.Stats;

namespace WarGame.Core.Systems;

/// <summary>Um conselho da prancheta: a frase que o estado-maior escreve à margem do desenho. `Bad` é o
/// que está mal a sério — o que se pinta a vermelho e o que o jogador tem de ver antes de assinar.</summary>
public readonly record struct DesignNote(string Text, bool Bad);

/// <summary>O que um desenho de divisão dá, antes de existir: a conta de batalhões, o preço, os homens,
/// os números de combate e o que a prancheta tem a dizer sobre aquilo.</summary>
/// <param name="Line">Batalhões de linha (os que fazem o peso).</param>
/// <param name="Support">Companhias de apoio (as que dão jeito).</param>
/// <param name="Cost">Pontos de produção.</param>
/// <param name="Manpower">Homens que a entrega tira ao país.</param>
/// <param name="Speed">Velocidade da divisão: a do batalhão mais lento.</param>
/// <param name="Slowest">Quem é esse batalhão — é a ele que se corta para a divisão andar.</param>
/// <param name="Stats">Ficha de combate somada (a mesma que o CombatSystem lê).</param>
/// <param name="Role">Para que serve, lido dos números.</param>
/// <param name="Notes">Os conselhos à margem.</param>
public sealed record DesignSheet(int Line, int Support, float Cost, float Manpower, float Speed,
    string Slowest, StatBlock Stats, string Role, IReadOnlyList<DesignNote> Notes)
{
    public int Battalions => Line + Support;
    public bool Bad => Notes.Any(n => n.Bad);
}

/// <summary>A prancheta do estado-maior: o que um desenho de divisão dá e o que se lhe tem a dizer.
///
/// O desenhador era uma caixa de SpinBoxes com o custo em baixo — punha-se 6 de infantaria e 2 de
/// artilharia às cegas e só se sabia o que aquilo dava depois de a divisão estar feita e no mapa. No HoI4
/// desenhar uma divisão é meia campanha: vê-se o que cada batalhão acrescenta à ficha, vê-se a velocidade
/// cair quando se lhe mete peso, e a prancheta avisa quando a coisa não fura blindagem nenhuma. É isso que
/// aqui se recria — a conta é a mesma do combate (DivisionStatCache), o que se acrescenta é a leitura.
///
/// Nada disto é decidido em código: os limites e os limiares são regras (design_*), o papel sai dos
/// números e os nomes saem das tabelas. Estado derivado: não guarda nada, não é ISystem.</summary>
public static class TemplateDesign
{
    /// <summary>Quantos batalhões de linha cabem numa divisão.</summary>
    public static int LineMax(World w) => (int)w.Rule("design_line_max", 24f);
    /// <summary>Quantas companhias de apoio cabem numa divisão.</summary>
    public static int SupportMax(World w) => (int)w.Rule("design_support_max", 5f);

    /// <summary>É de apoio? A categoria vem da tabela unit_type — não há lista nenhuma no código.</summary>
    public static bool IsSupport(World w, int unitTypeId)
    {
        try { return w.Units.GetUnitType(unitTypeId).Category == "support"; } catch { return false; }
    }

    /// <summary>A prancheta de um desenho que ainda não é template nenhum.</summary>
    public static DesignSheet Of(World w, int countryId, IReadOnlyList<(int UnitTypeId, int Qty)> units)
    {
        int line = 0, support = 0;
        float cost = 0f, slowest = float.MaxValue;
        string slowName = "";
        foreach (var (id, qty) in units)
        {
            if (qty <= 0) continue;
            UnitType u;
            try { u = w.Units.GetUnitType(id); } catch { continue; }
            if (u.Category == "support") support += qty; else line += qty;
            cost += u.Cost * qty;
            if (u.Mobility < slowest) { slowest = u.Mobility; slowName = u.Name; }
        }
        var st = w.Stats.Preview(units.Where(u => u.Qty > 0).ToList());
        float speed = line + support == 0 ? 0f : st["mobility"];
        var sheet = new DesignSheet(line, support, cost, cost * w.Rule("manpower_per_cost", 500f),
                                    speed, slowName, st, Role(w, st, line, support), Array.Empty<DesignNote>());
        return sheet with { Notes = Advice(w, countryId, sheet) };
    }

    /// <summary>A prancheta de um template que já existe — para se ler a ficha de um modelo antigo com os
    /// mesmos olhos com que se desenha um novo.</summary>
    public static DesignSheet OfTemplate(World w, int templateId)
    {
        try
        {
            var t = w.Units.GetTemplate(templateId);
            return Of(w, t.CountryId, t.Units);
        }
        catch (InvalidOperationException)
        {
            return Of(w, 0, Array.Empty<(int, int)>());
        }
    }

    /// <summary>Para que serve isto, lido dos números e não do nome: dureza acima do limiar é punho
    /// blindado; rotura muito acima da defesa é tropa de assalto; defesa muito acima da rotura é tropa de
    /// linha; o resto é infantaria de manobra. Um desenho vazio não é nada.</summary>
    public static string Role(World w, StatBlock st, int line, int support)
    {
        if (line + support == 0) return "prancheta vazia";
        if (line == 0) return "só apoios — não é divisão nenhuma";
        float hard = st["hardness"], brk = st["breakthrough"], def = MathF.Max(0.001f, st["defense"]);
        if (hard >= w.Rule("design_role_hardness", 0.4f)) return "punho blindado";
        if (brk / def >= w.Rule("design_role_punch", 1.15f)) return "tropa de assalto";
        if (def / MathF.Max(0.001f, brk) >= w.Rule("design_role_wall", 1.35f)) return "tropa de linha";
        return "infantaria de manobra";
    }

    /// <summary>O que o estado-maior escreve à margem. São contas sobre a ficha, não gostos: cada uma tem
    /// um limiar em regra e uma frase que diz o que acontece no campo se ficar assim.</summary>
    public static List<DesignNote> Advice(World w, int countryId, DesignSheet s)
    {
        var notes = new List<DesignNote>();
        int lineMax = LineMax(w), supMax = SupportMax(w);
        if (s.Battalions == 0) { notes.Add(new DesignNote("prancheta vazia: escolhe batalhões à esquerda", true)); return notes; }
        if (s.Line == 0) notes.Add(new DesignNote("só companhias de apoio: sem batalhões de linha não há quem segure a frente", true));
        if (s.Line > lineMax) notes.Add(new DesignNote($"{s.Line} batalhões de linha: o máximo são {lineMax}", true));
        if (s.Support > supMax) notes.Add(new DesignNote($"{s.Support} companhias de apoio: o máximo são {supMax}", true));
        if (s.Line > 0 && s.Line < (int)w.Rule("design_line_min", 3f))
            notes.Add(new DesignNote($"só {s.Line} batalhões de linha: uma divisão destas parte-se no primeiro embate", false));
        if (s.Support == 0 && s.Line > 0)
            notes.Add(new DesignNote("sem companhias de apoio: a artilharia e os sapadores da retaguarda não vão com ela", false));

        float pierce = s.Stats["piercing"], armor = s.Stats["armor"];
        if (pierce < w.Rule("design_pierce_floor", 5f))
            notes.Add(new DesignNote($"perfuração {pierce:0.0}: os carros do outro lado passam-lhe por cima", true));
        if (armor > 0f && s.Stats["hardness"] < w.Rule("design_role_hardness", 0.4f) * 0.5f)
            notes.Add(new DesignNote("blindagem a mais para tão poucos veículos: paga-se o aço e leva-se o tiro na carne", false));
        if (s.Speed > 0f && s.Speed < w.Rule("design_speed_floor", 3f))
            notes.Add(new DesignNote($"anda a {s.Speed:0.0}: {(s.Slowest.Length > 0 ? s.Slowest + " atrasa a divisão inteira" : "é o batalhão mais lento que manda")}", false));
        if (s.Stats["fuel_use"] > 0f)
            notes.Add(new DesignNote($"bebe {s.Stats["fuel_use"]:0.0} de combustível por dia: sem barris fica parada", false));

        // e o país: uma divisão que ele não pode pagar é um desenho bonito e nada mais
        if (w.Countries.TryGetValue(countryId, out var c))
        {
            if (c.Manpower >= 0f && c.Manpower < s.Manpower)
                notes.Add(new DesignNote($"faltam homens: pede {s.Manpower / 1000f:0.0}k e há {c.Manpower / 1000f:0.0}k", true));
            float best = 0f;
            foreach (var t in Templates(w, countryId))
                if (t.Id != 0) best = MathF.Max(best, w.Stats.Get(t.Id)["defense"] + w.Stats.Get(t.Id)["breakthrough"]);
            float mine = s.Stats["defense"] + s.Stats["breakthrough"];
            if (best > 0f && mine < best * 0.75f)
                notes.Add(new DesignNote("mais fraca do que os modelos que já tens: vale mais encomendar os antigos", false));
        }
        return notes;
    }

    private static IReadOnlyList<DivisionTemplate> Templates(World w, int countryId)
    {
        try { return w.Units.GetTemplates(countryId); } catch { return Array.Empty<DivisionTemplate>(); }
    }

    /// <summary>O desenho em palavras, para a chapa e para a prova headless.</summary>
    public static string Short(DesignSheet s) =>
        $"{s.Line}+{s.Support} batalhões · {s.Role} · custo {s.Cost:0.0} · {s.Manpower / 1000f:0.0}k homens"
        + $" · {s.Notes.Count} conselho{(s.Notes.Count == 1 ? "" : "s")}{(s.Bad ? " (um deles grave)" : "")}";
}
