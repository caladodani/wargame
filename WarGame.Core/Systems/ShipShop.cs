using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>O estaleiro: onde se desenha um navio à peça (HoI4: ship designer).
///
/// O mar deixou de ser um número quando apareceram as classes de casco, mas o casco continuava a vir feito
/// da tabela: escolhia-se de uma lista de sete e mais nada. Uma marinha com trinta anos de investigação
/// navegava exactamente na mesma fragata do vizinho que nunca investira um tostão, e a única decisão naval
/// que sobrava era quantos comprar. No HoI4 o casco é só o princípio — o que se lhe mete em cima é que
/// decide se aquilo é um caça-submarinos que limpa uma bacia, um lança-mísseis que fecha um mar de longe ou
/// um porta-helicópteros.
///
/// A conta é a mesma da oficina de aviões, e é uma soma: o casco dá a base, cada peça SOMA a sua coluna
/// (ship_module), e o que sai é um ShipClassDef igual aos da tabela — daí para a frente o desenho é um
/// casco como os outros para TODO o jogo (compra-se no estaleiro, vai às missões, bloqueia, escolta, caça
/// submarinos, esconde-se se levar com que se calar, e aparece nos contadores do mapa). Não há um único
/// navio escrito em código: as ranhuras são ship_class.slots, as peças são linhas, e o que cada peça vale
/// são as colunas dela.
///
/// O que se guarda no save é a ESCOLHA (casco + peça de cada ranhura), nunca os números: assim uma peça
/// reafinada na tabela vale logo em todos os desenhos que a levam.
///
/// Estado derivado: não guarda nada de seu, não entra no save e não é ISystem — o que é do jogador vive no
/// World.ShipDesigns e é o save que o leva.</summary>
public static class ShipShop
{
    /// <summary>O id por que o resto do jogo conhece um casco desenhado. Prefixo próprio, e diferente do
    /// dos aviões, para nunca chocar com um id de tabela nem com um desenho de céu.</summary>
    public static string ClassId(int designId) => "cas:" + designId;

    /// <summary>É um casco desenhado em jogo (e não uma linha de ship_class)?</summary>
    public static bool IsDesign(string classId) => classId.StartsWith("cas:", StringComparison.Ordinal);

    /// <summary>O número do desenho lido do id ("cas:7" → 7); 0 quando não é um desenho.</summary>
    public static int DesignId(string classId) =>
        IsDesign(classId) && int.TryParse(classId.AsSpan(4), out int id) ? id : 0;

    /// <summary>As ranhuras deste casco, pela ordem em que se vêem. Vazio = casco que não se desenha (a
    /// lista está na coluna slots, não em código).</summary>
    public static List<string> Slots(World w, string chassis)
    {
        if (chassis.Length == 0 || !w.ShipClasses.TryGetValue(chassis, out var d) || !d.Designable)
            return new List<string>();
        return d.Slots.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>Os cascos que se podem levar à prancheta, pela ordem da tabela.</summary>
    public static List<ShipClassDef> Chassis(World w) =>
        w.ShipClasses.Values.Where(d => d.Designable && !IsDesign(d.Id)).OrderBy(d => d.Sort)
            .ThenBy(d => d.Id, StringComparer.Ordinal).ToList();

    /// <summary>As peças que cabem nesta ranhura e que este país já tem investigadas. Sem país (id 0 ou
    /// desconhecido) mostram-se todas — é o que o mundo de teste e a ficha do vizinho pedem.</summary>
    public static List<ShipModuleDef> Fit(World w, int countryId, string slot)
    {
        w.Countries.TryGetValue(countryId, out var c);
        return w.ShipModules.Values
            .Where(m => m.Slot == slot && (m.TechId.Length == 0 || c is null || c.Techs.Contains(m.TechId)))
            .OrderBy(m => m.Sort).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>O que falta investigar para pôr esta peça (vazio se já se pode). Serve para a prancheta
    /// mostrar a peça fechada com o nome da investigação em vez de a esconder: esconder uma peça é esconder
    /// uma razão para investigar.</summary>
    public static string Locked(World w, int countryId, string moduleId)
    {
        if (!w.ShipModules.TryGetValue(moduleId, out var m) || m.TechId.Length == 0) return "";
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Techs.Contains(m.TechId)) return "";
        return w.Techs.TryGetValue(m.TechId, out var t) ? t.Name : m.TechId;
    }

    /// <summary>Os números que este desenho dá: o casco mais a soma das peças, nada abaixo de zero (o
    /// esconderijo é fatia e nunca passa de um casco inteiro). É esta ficha que o estaleiro mostra, que o
    /// combate lê e que o mapa desenha — a mesma para todos.</summary>
    public static ShipClassDef Build(World w, ShipDesign d)
    {
        var slots = Slots(w, d.Chassis);
        if (!w.ShipClasses.TryGetValue(d.Chassis, out var b))
            b = new ShipClassDef(d.Chassis, d.Name, "⚓", "escolta", 1f, 1f, 1f, 1f, 1f, 1f, 1f, false, "", 0, "barco");

        float cost = b.Cost, upkeep = b.Upkeep, battle = b.Battle, screen = b.Screen, blockade = b.Blockade;
        float escort = b.Escort, patrol = b.Patrol, deck = b.Deck, stealth = b.Stealth, asw = b.Asw;
        foreach (var m in Fitted(w, d, slots))
        {
            cost += m.Cost; upkeep += m.Upkeep; battle += m.Battle; screen += m.Screen; blockade += m.Blockade;
            escort += m.Escort; patrol += m.Patrol; deck += m.Deck; stealth += m.Stealth; asw += m.Asw;
        }
        float F(float v) => MathF.Max(0f, v);
        return new ShipClassDef(ClassId(d.Id), d.Name.Length > 0 ? d.Name : b.Name, b.Icon, b.Role,
                                MathF.Max(0.05f, cost), F(upkeep), F(battle), F(screen), F(blockade),
                                F(escort), F(patrol), false, Note(w, d, b), 900 + d.Id, Glyph(w, d, b),
                                F(deck), Math.Clamp(stealth, 0f, 0.99f), F(asw), b.Slots);
    }

    /// <summary>As peças mesmo montadas, pela ordem das ranhuras: salta ranhura vazia, peça desconhecida e
    /// peça que não é daquela ranhura (um save velho ou uma tabela mexida não podem dar números falsos).</summary>
    public static List<ShipModuleDef> Fitted(World w, ShipDesign d, List<string>? slots = null)
    {
        slots ??= Slots(w, d.Chassis);
        var got = new List<ShipModuleDef>();
        for (int i = 0; i < slots.Count && i < d.Modules.Count; i++)
        {
            string id = d.Modules[i];
            if (id.Length == 0 || !w.ShipModules.TryGetValue(id, out var m) || m.Slot != slots[i]) continue;
            got.Add(m);
        }
        return got;
    }

    /// <summary>A chapa do desenho: a da peça mais cara que ele leva, e a do casco quando não leva nenhuma.
    /// Assim o navio de casa vê-se pelo que o faz diferente — no estaleiro e no contador do mapa.</summary>
    private static string Glyph(World w, ShipDesign d, ShipClassDef b)
    {
        var heavy = Fitted(w, d).Where(m => m.Glyph.Length > 0).OrderByDescending(m => m.Cost)
            .ThenBy(m => m.Sort).FirstOrDefault();
        return heavy?.Glyph ?? b.Glyph;
    }

    private static string Note(World w, ShipDesign d, ShipClassDef b)
    {
        var parts = Fitted(w, d).Select(m => m.Name.ToLowerInvariant()).ToList();
        return parts.Count == 0
            ? $"Desenhado em casa sobre {b.Name.ToLowerInvariant()}, sem peça nenhuma montada."
            : $"Desenhado em casa sobre {b.Name.ToLowerInvariant()}: {string.Join(", ", parts)}.";
    }

    /// <summary>Põe (ou repõe) o desenho na lista dos cascos do mundo. Chamado a criar, a redesenhar e a
    /// abrir um save — é o único sítio onde um desenho vira classe de navio.</summary>
    public static void Register(World w, ShipDesign d)
    {
        if (!w.ShipDesigns.Contains(d)) w.ShipDesigns.Add(d);
        w.ShipClasses[ClassId(d.Id)] = Build(w, d);
    }

    /// <summary>Os desenhos de um país, do mais velho para o mais novo.</summary>
    public static List<ShipDesign> Of(World w, int countryId) =>
        w.ShipDesigns.Where(d => d.CountryId == countryId).OrderBy(d => d.Id).ToList();

    /// <summary>O número do desenho a seguir: nunca reaproveita um que já foi usado, para um save velho
    /// nunca acordar com navios de outro desenho no porto.</summary>
    public static int NextId(World w) => w.ShipDesigns.Count == 0 ? 1 : w.ShipDesigns.Max(d => d.Id) + 1;

    /// <summary>Milhas navegadas que a assinatura custa: menos para mexer num casco que já existe.</summary>
    public static float Price(World w, bool editing) =>
        w.Rule(editing ? "ship_design_edit_xp" : "ship_design_xp", editing ? 12f : 30f);

    /// <summary>O que impede este desenho de ser assinado, em palavras — null quando está pronto. É a mesma
    /// porta para o comando, para a prancheta e para a IA: a resposta tem de ser uma só.</summary>
    public static string? Check(World w, int countryId, string chassis, IReadOnlyList<string> modules,
                                int editing = 0)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return "País desconhecido";
        if (!w.ShipClasses.TryGetValue(chassis, out var b)) return "Casco desconhecido";
        if (!b.Designable || IsDesign(chassis)) return $"{b.Name} não se desenha: vem feito do estaleiro";
        var slots = Slots(w, chassis);
        if (modules.Count != slots.Count) return $"{b.Name} tem {slots.Count} ranhuras, não {modules.Count}";

        for (int i = 0; i < slots.Count; i++)
        {
            string id = modules[i];
            w.ShipSlotDefs.TryGetValue(slots[i], out var slot);
            string slotName = slot?.Name ?? slots[i];
            if (id.Length == 0)
            {
                if (slot is not null && slot.Required) return $"Ranhura de {slotName.ToLowerInvariant()} vazia";
                continue;
            }
            if (!w.ShipModules.TryGetValue(id, out var m)) return "Peça desconhecida";
            if (m.Slot != slots[i]) return $"{m.Name} não entra numa ranhura de {slotName.ToLowerInvariant()}";
            if (Locked(w, countryId, id) is { Length: > 0 } tech) return $"{m.Name} precisa de {tech}";
        }

        if (editing == 0)
        {
            int max = (int)w.Rule("ship_design_max", 12f);
            if (Of(w, countryId).Count >= max) return $"O estaleiro já tem {max} desenhos: apaga um antes";
        }
        else if (!w.ShipDesigns.Any(d => d.Id == editing && d.CountryId == countryId))
            return "Esse desenho não é desta casa";

        float price = Price(w, editing != 0);
        if (c.NavyXp < price) return $"Faltam milhas: a prancheta pede {price:0} de experiência naval";
        return null;
    }

    /// <summary>Os conselhos à margem, do Core e não do ecrã: o que este casco faz bem, o que não faz de
    /// todo e o que está a pagar sem usar. Bad a true é o que impede a assinatura.</summary>
    public static List<(string Text, bool Bad)> Advice(World w, int countryId, string chassis,
                                                      IReadOnlyList<string> modules)
    {
        var notes = new List<(string, bool)>();
        var slots = Slots(w, chassis);
        if (slots.Count == 0) return notes;

        var draft = new ShipDesign { Chassis = chassis, Modules = modules.ToList() };
        var sheet = Build(w, draft);

        for (int i = 0; i < slots.Count && i < modules.Count; i++)
        {
            if (modules[i].Length > 0) continue;
            if (w.ShipSlotDefs.TryGetValue(slots[i], out var s) && s.Required)
                notes.Add(($"Sem {s.Name.ToLowerInvariant()} não larga do cais.", true));
        }

        int empty = 0;
        for (int i = 0; i < slots.Count; i++) if (i >= modules.Count || modules[i].Length == 0) empty++;
        if (empty > 0 && !notes.Any(n => n.Item2))
            notes.Add(($"{empty} ranhura{(empty == 1 ? "" : "s")} por encher: o casco navega, mas leva mar onde podia levar guerra.", false));

        float war = sheet.Battle + sheet.Blockade + sheet.Escort + sheet.Patrol + sheet.Asw;
        if (war < 0.5f) notes.Add(("Não serve para tarefa nenhuma: leva-o ao mar e ele fica a ver.", false));
        if (sheet.Battle > 1.5f && sheet.Screen < 0.5f)
            notes.Add(("Peso de linha sem couraça nenhuma: aço caro à espera do primeiro torpedo.", false));
        if (sheet.Asw > 1.5f && sheet.Escort < 0.8f)
            notes.Add(("Ouve tudo o que anda por baixo e não protege comboio nenhum: caçador, não escolta.", false));
        if (sheet.Stealth > 0f && sheet.Asw > 0.5f)
            notes.Add(("Esconde-se e caça ao mesmo tempo: quem quer ouvir tem de fazer barulho.", false));
        if (sheet.Blockade > 1.5f && sheet.Escort < 0.3f)
            notes.Add(("Feito para cortar o mar aos outros — o nosso comércio que se defenda sozinho.", false));
        if (sheet.IsCarrier) notes.Add(($"Leva {sheet.Deck:0.#} asas ao mar.", false));
        if (sheet.IsSub) notes.Add(($"Anda escondido: {sheet.Stealth:0%} dele não leva tiro enquanto ninguém o vir.", false));
        notes.Add(($"Custo {w.Rule("naval_ship_cost", 90f) * sheet.Cost:0} por casco, sustento ×{sheet.Upkeep:0.0}.", false));
        return notes;
    }

    /// <summary>Uma linha para a prova headless e para a ficha: o que este casco é, em números.</summary>
    public static string Short(World w, ShipClassDef d) =>
        $"combate {d.Battle:0.0}, couraça {d.Screen:0.0}, bloqueio {d.Blockade:0.0}, escolta {d.Escort:0.0}, "
        + $"patrulha {d.Patrol:0.0}, sonar {d.Asw:0.0}"
        + (d.IsCarrier ? $", convés {d.Deck:0.#}" : "") + (d.IsSub ? $", esconde {d.Stealth:0%}" : "");
}
