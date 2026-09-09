using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A oficina: onde se desenha um avião à peça (HoI4: aircraft designer).
///
/// O céu deixou de ser um número quando apareceram os modelos (plane_class, 0.3.65), mas o modelo continuava
/// a vir feito da tabela: escolhia-se de uma lista de nove e mais nada. Um país que investisse trinta anos
/// em aviação voava exactamente no mesmo caça do vizinho que não investira nada. No HoI4 a fuselagem é só
/// o começo — o que decide se aquilo é um caça que ganha o céu, um torpedeiro que abre esquadras ao meio ou
/// um camião com asas é o que se lhe mete nas ranhuras.
///
/// A conta é uma soma e nada mais: a fuselagem dá a base, cada peça SOMA a sua coluna (plane_module), e o
/// que sai é um PlaneClassDef igual aos da tabela — a partir daí o desenho é um modelo como os outros para
/// todo o jogo (compra-se no hangar, escolhe-se para as missões, dorme nos campos, cai nos combates). Não
/// há um único avião escrito em código: as ranhuras são plane_class.slots, as peças são linhas, e o que
/// cada peça vale são as colunas dela.
///
/// O que se guarda no save é a ESCOLHA (fuselagem + peça de cada ranhura), nunca os números: assim uma peça
/// reafinada na tabela vale logo em todos os desenhos que a levam, como acontece às marcas de material.
///
/// Estado derivado: não guarda nada de seu, não entra no save e não é ISystem — o que é do jogador vive no
/// World.PlaneDesigns e é o save que o leva.</summary>
public static class PlaneShop
{
    /// <summary>O id por que o resto do jogo conhece um desenho. Prefixo próprio para nunca chocar com um
    /// id de tabela — e para se saber, só de olhar, que aquele avião foi desenhado em casa.</summary>
    public static string ClassId(int designId) => "des:" + designId;

    /// <summary>É um modelo desenhado em jogo (e não uma linha de plane_class)?</summary>
    public static bool IsDesign(string classId) => classId.StartsWith("des:", StringComparison.Ordinal);

    /// <summary>As ranhuras desta fuselagem, pela ordem em que se vêem. Vazio = fuselagem que não se
    /// desenha (a lista está na coluna slots, não em código).</summary>
    public static List<string> Slots(World w, string chassis)
    {
        if (chassis.Length == 0 || !w.PlaneClasses.TryGetValue(chassis, out var d) || !d.Designable)
            return new List<string>();
        return d.Slots.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>As fuselagens que se podem levar à prancheta, pela ordem da tabela.</summary>
    public static List<PlaneClassDef> Chassis(World w) =>
        w.PlaneClasses.Values.Where(d => d.Designable && !IsDesign(d.Id)).OrderBy(d => d.Sort)
            .ThenBy(d => d.Id, StringComparer.Ordinal).ToList();

    /// <summary>As peças que cabem nesta ranhura e que este país já tem investigadas. Sem país (id 0 ou
    /// desconhecido) mostram-se todas — é o que o mundo de teste e a ficha do vizinho pedem.</summary>
    public static List<PlaneModuleDef> Fit(World w, int countryId, string slot)
    {
        w.Countries.TryGetValue(countryId, out var c);
        return w.PlaneModules.Values
            .Where(m => m.Slot == slot && (m.TechId.Length == 0 || c is null || c.Techs.Contains(m.TechId)))
            .OrderBy(m => m.Sort).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>O que falta investigar para pôr esta peça (vazio se já se pode). Serve para a prancheta
    /// mostrar a peça fechada com o nome da investigação em vez de a esconder.</summary>
    public static string Locked(World w, int countryId, string moduleId)
    {
        if (!w.PlaneModules.TryGetValue(moduleId, out var m) || m.TechId.Length == 0) return "";
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Techs.Contains(m.TechId)) return "";
        return w.Techs.TryGetValue(m.TechId, out var t) ? t.Name : m.TechId;
    }

    /// <summary>Os números que este desenho dá: a fuselagem mais a soma das peças, nada abaixo de zero.
    /// É esta ficha que o hangar mostra e que o combate lê — a mesma para todos.</summary>
    public static PlaneClassDef Build(World w, PlaneDesign d)
    {
        var slots = Slots(w, d.Chassis);
        if (!w.PlaneClasses.TryGetValue(d.Chassis, out var b))
            b = new PlaneClassDef(d.Chassis, d.Name, "✈", "caca", 1f, 1f, 1f, 1f, 1f, 1f, 1f, false, "", 0, "asa");

        float cost = b.Cost, upkeep = b.Upkeep, air = b.Air, sup = b.Superiority, sp = b.Support;
        float bomb = b.Bombing, tr = b.Transport, nav = b.Naval, range = b.RangeKm;
        int deck = b.Deck ? 1 : 0;
        foreach (var m in Fitted(w, d, slots))
        {
            cost += m.Cost; upkeep += m.Upkeep; air += m.Air; sup += m.Superiority; sp += m.Support;
            bomb += m.Bombing; tr += m.Transport; nav += m.Naval; range += m.RangeKm;
            deck += m.Deck;
        }
        float F(float v) => MathF.Max(0f, v);
        string glyph = Glyph(w, d, b);
        return new PlaneClassDef(ClassId(d.Id), d.Name.Length > 0 ? d.Name : b.Name, b.Icon, b.Role,
                                 MathF.Max(0.05f, cost), F(upkeep), F(air), F(sup), F(sp), F(bomb), F(tr),
                                 false, Note(w, d, b), 900 + d.Id, glyph, F(range), F(nav), deck > 0, b.Slots);
    }

    /// <summary>As peças mesmo montadas, pela ordem das ranhuras: salta ranhura vazia, peça desconhecida e
    /// peça que não é daquela ranhura (um save velho ou uma tabela mexida não podem dar números falsos).</summary>
    public static List<PlaneModuleDef> Fitted(World w, PlaneDesign d, List<string>? slots = null)
    {
        slots ??= Slots(w, d.Chassis);
        var got = new List<PlaneModuleDef>();
        for (int i = 0; i < slots.Count && i < d.Modules.Count; i++)
        {
            string id = d.Modules[i];
            if (id.Length == 0 || !w.PlaneModules.TryGetValue(id, out var m) || m.Slot != slots[i]) continue;
            got.Add(m);
        }
        return got;
    }

    /// <summary>A chapa do desenho: a da peça mais cara que ele leva, e a da fuselagem quando não leva
    /// nenhuma. Assim o avião de casa vê-se no hangar pelo que o faz diferente.</summary>
    private static string Glyph(World w, PlaneDesign d, PlaneClassDef b)
    {
        var heavy = Fitted(w, d).Where(m => m.Glyph.Length > 0).OrderByDescending(m => m.Cost)
            .ThenBy(m => m.Sort).FirstOrDefault();
        return heavy?.Glyph ?? b.Glyph;
    }

    private static string Note(World w, PlaneDesign d, PlaneClassDef b)
    {
        var parts = Fitted(w, d).Select(m => m.Name.ToLowerInvariant()).ToList();
        return parts.Count == 0
            ? $"Desenhado em casa sobre {b.Name.ToLowerInvariant()}, sem peça nenhuma montada."
            : $"Desenhado em casa sobre {b.Name.ToLowerInvariant()}: {string.Join(", ", parts)}.";
    }

    /// <summary>Põe (ou repõe) o desenho na lista dos modelos do mundo. Chamado a criar, a redesenhar e a
    /// abrir um save — é o único sítio onde um desenho vira modelo.</summary>
    public static void Register(World w, PlaneDesign d)
    {
        if (!w.PlaneDesigns.Contains(d)) w.PlaneDesigns.Add(d);
        w.PlaneClasses[ClassId(d.Id)] = Build(w, d);
    }

    /// <summary>Os desenhos de um país, do mais velho para o mais novo.</summary>
    public static List<PlaneDesign> Of(World w, int countryId) =>
        w.PlaneDesigns.Where(d => d.CountryId == countryId).OrderBy(d => d.Id).ToList();

    /// <summary>O número do desenho a seguir: nunca reaproveita um que já foi usado, para um save velho
    /// nunca acordar com aviões de outro desenho no hangar.</summary>
    public static int NextId(World w) => w.PlaneDesigns.Count == 0 ? 1 : w.PlaneDesigns.Max(d => d.Id) + 1;

    /// <summary>Experiência de aviação que a assinatura custa: menos para mexer num que já existe.</summary>
    public static float Price(World w, bool editing) =>
        w.Rule(editing ? "plane_design_edit_xp" : "plane_design_xp", editing ? 10f : 25f);

    /// <summary>O que impede este desenho de ser assinado, em palavras — null quando está pronto. É a
    /// mesma porta para o comando, para a prancheta e para a IA: a resposta tem de ser uma só.</summary>
    public static string? Check(World w, int countryId, string chassis, IReadOnlyList<string> modules,
                                int editing = 0)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return "País desconhecido";
        if (!w.PlaneClasses.TryGetValue(chassis, out var b)) return "Fuselagem desconhecida";
        if (!b.Designable || IsDesign(chassis)) return $"{b.Name} não se desenha: vem feito da fábrica";
        var slots = Slots(w, chassis);
        if (modules.Count != slots.Count) return $"{b.Name} tem {slots.Count} ranhuras, não {modules.Count}";

        for (int i = 0; i < slots.Count; i++)
        {
            string id = modules[i];
            w.PlaneSlotDefs.TryGetValue(slots[i], out var slot);
            string slotName = slot?.Name ?? slots[i];
            if (id.Length == 0)
            {
                if (slot is not null && slot.Required) return $"Ranhura de {slotName.ToLowerInvariant()} vazia";
                continue;
            }
            if (!w.PlaneModules.TryGetValue(id, out var m)) return "Peça desconhecida";
            if (m.Slot != slots[i]) return $"{m.Name} não entra numa ranhura de {slotName.ToLowerInvariant()}";
            if (Locked(w, countryId, id) is { Length: > 0 } tech) return $"{m.Name} precisa de {tech}";
        }

        if (editing == 0)
        {
            int max = (int)w.Rule("plane_design_max", 12f);
            if (Of(w, countryId).Count >= max) return $"A oficina já tem {max} desenhos: apaga um antes";
        }
        else if (!w.PlaneDesigns.Any(d => d.Id == editing && d.CountryId == countryId))
            return "Esse desenho não é desta casa";

        float price = Price(w, editing != 0);
        if (c.AirXp < price) return $"Faltam horas de voo: a prancheta pede {price:0} de experiência aérea";
        return null;
    }

    /// <summary>Os conselhos à margem, do Core e não do ecrã: o que este desenho faz bem, o que não faz de
    /// todo e o que está a pagar sem usar. Bad a true é o que impede a assinatura.</summary>
    public static List<(string Text, bool Bad)> Advice(World w, int countryId, string chassis,
                                                      IReadOnlyList<string> modules)
    {
        var notes = new List<(string, bool)>();
        var slots = Slots(w, chassis);
        if (slots.Count == 0) return notes;

        var draft = new PlaneDesign { Chassis = chassis, Modules = modules.ToList() };
        var got = Fitted(w, draft, slots);
        var sheet = Build(w, draft);

        for (int i = 0; i < slots.Count && i < modules.Count; i++)
        {
            if (modules[i].Length > 0) continue;
            if (w.PlaneSlotDefs.TryGetValue(slots[i], out var s) && s.Required)
                notes.Add(($"Sem {s.Name.ToLowerInvariant()} não levanta.", true));
        }

        int empty = 0;
        for (int i = 0; i < slots.Count; i++) if (i >= modules.Count || modules[i].Length == 0) empty++;
        if (empty > 0 && !notes.Any(n => n.Item2))
            notes.Add(($"{empty} ranhura{(empty == 1 ? "" : "s")} por encher: o avião voa, mas leva ar onde podia levar guerra.", false));

        float war = sheet.Superiority + sheet.Support + sheet.Bombing + sheet.Transport + sheet.Naval;
        if (war < 0.3f) notes.Add(("Não serve para tarefa nenhuma: leva-o ao céu e ele fica a ver.", false));
        if (sheet.Superiority > 1.2f && sheet.Air < 1f)
            notes.Add(("Quer ganhar o céu e não tem com que se defender nele — é abate certo.", false));
        if (sheet.Naval > 1f && sheet.Bombing < 0.5f)
            notes.Add(("É um torpedeiro: sobre terra não faz nada de jeito.", false));
        if (sheet.Transport > 1f && war - sheet.Transport > 0.8f)
            notes.Add(("Metade dele é carga e a outra metade é guerra: paga as duas e não faz nenhuma bem.", false));
        if (got.Any(m => m.Deck > 0) && w.PlaneClasses.TryGetValue(chassis, out var basec) && basec.Deck)
            notes.Add(("Já cabia num convés sem o gancho: essa peça está paga por nada.", false));
        if (sheet.Deck) notes.Add(("Cabe num porta-aviões.", false));
        notes.Add(($"Alcance {sheet.RangeKm:0} km, custo {w.Rule("air_wing_cost", 60f) * sheet.Cost:0} por asa, "
                   + $"estadia ×{sheet.Upkeep:0.0}.", false));
        return notes;
    }

    /// <summary>Uma linha para a prova headless e para a ficha: o que este desenho é, em números.</summary>
    public static string Short(World w, PlaneClassDef d) =>
        $"combate {d.Air:0.0}, céu {d.Superiority:0.0}, apoio {d.Support:0.0}, bombas {d.Bombing:0.0}, "
        + $"mar {d.Naval:0.0}, carga {d.Transport:0.0}, {d.RangeKm:0} km";
}
