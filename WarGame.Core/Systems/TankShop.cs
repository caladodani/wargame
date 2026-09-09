using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A prancheta dos carros: desenhar um carro de combate à peça (HoI4: tank designer).
///
/// É a terceira e última prancheta do jogo — o avião saiu em 0.3.70, o navio em 0.3.72 — e é a que fechava
/// o ciclo que já lá estava: investigação → marca de material → fábrica → armazém → divisão que se bate.
/// Até aqui essa ladeira era a mesma para toda a gente. Toda a gente subia as quatro gerações da tabela e
/// toda a gente acabava no mesmo carro; a única decisão do jogador era quando investigar.
///
/// O que a prancheta faz é deixar o país desenhar a geração SEGUINTE com as mãos dele. E o que sai dela NÃO
/// é uma classe à parte: é uma linha de equipment_mark com dono (OwnerId), com a mark a seguir à última da
/// tabela daquele tipo de unidade. É essa a ideia toda — a fábrica reafina-se para ela sozinha (Retool), o
/// armazém mistura-a na pilha (Blend), a divisão bate-se com ela (PowerMult) e gasta-a mais devagar
/// (WearMult), sem que uma única linha do resto do jogo saiba que aquele carro foi desenhado em casa.
///
/// A conta é uma soma como nas outras duas pranchetas: o casco dá a base e cada peça SOMA a sua coluna.
/// Custo, força e desgaste — três números, porque três são os que a ladeira das marcas usa. Nenhum carro
/// está escrito em código: os cascos são linhas de tank_chassis, as ranhuras são a coluna slots e as peças
/// são linhas de tank_module.
///
/// A lei que segura tudo de pé é a aresta (regra tank_design_edge): o carro desenhado tem de bater a melhor
/// marca que a fábrica já sabe fazer. Sem ela assinava-se um carro pior do que o da tabela, a ladeira das
/// marcas deixava de subir e a tropa acordava com material que ninguém lhe prometeu. Com ela, a liberdade
/// do jogador está em COMO bater: mais canhão e menos aço, um carro caro que dura, ou um carro barato que
/// se faz aos molhos.
///
/// Estado derivado: não guarda nada de seu e não é ISystem — o que é do jogador vive no World.TankDesigns e
/// é o save que o leva.</summary>
public static class TankShop
{
    /// <summary>O id da marca por que o resto do jogo conhece um carro desenhado. Prefixo próprio para
    /// nunca chocar com um id de tabela — e para se saber, só de olhar, que aquilo foi feito em casa.</summary>
    public static string MarkId(int designId) => "car:" + designId;

    /// <summary>É uma marca desenhada em jogo (e não uma linha de equipment_mark)?</summary>
    public static bool IsDesign(string markId) => markId.StartsWith("car:", StringComparison.Ordinal);

    /// <summary>As ranhuras deste casco, pela ordem em que se vêem. Vazio = casco que não se desenha (a
    /// lista está na coluna slots, não em código).</summary>
    public static List<string> Slots(World w, string chassis)
    {
        if (chassis.Length == 0 || !w.TankChassis.TryGetValue(chassis, out var d) || !d.Designable)
            return new List<string>();
        return d.Slots.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>Os cascos que se podem levar à prancheta, pela ordem da tabela.</summary>
    public static List<TankChassisDef> Chassis(World w) =>
        w.TankChassis.Values.Where(d => d.Designable).OrderBy(d => d.Sort)
            .ThenBy(d => d.Id, StringComparer.Ordinal).ToList();

    /// <summary>As peças que cabem nesta ranhura e que este país já tem investigadas. Sem país mostram-se
    /// todas — é o que o mundo de prova e a ficha do vizinho pedem.</summary>
    public static List<TankModuleDef> Fit(World w, int countryId, string slot)
    {
        w.Countries.TryGetValue(countryId, out var c);
        return w.TankModules.Values
            .Where(m => m.Slot == slot && (m.TechId.Length == 0 || c is null || c.Techs.Contains(m.TechId)))
            .OrderBy(m => m.Sort).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>O que falta investigar para pôr esta peça (vazio se já se pode). Serve para a prancheta
    /// mostrar a peça fechada com o nome da investigação em vez de a esconder.</summary>
    public static string Locked(World w, int countryId, string moduleId)
    {
        if (!w.TankModules.TryGetValue(moduleId, out var m) || m.TechId.Length == 0) return "";
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Techs.Contains(m.TechId)) return "";
        return w.Techs.TryGetValue(m.TechId, out var t) ? t.Name : m.TechId;
    }

    /// <summary>O mesmo para o casco: um casco pesado não se leva à prancheta sem a investigação dele.</summary>
    public static string ChassisLocked(World w, int countryId, string chassis)
    {
        if (!w.TankChassis.TryGetValue(chassis, out var b) || b.TechId.Length == 0) return "";
        if (!w.Countries.TryGetValue(countryId, out var c) || c.Techs.Contains(b.TechId)) return "";
        return w.Techs.TryGetValue(b.TechId, out var t) ? t.Name : b.TechId;
    }

    /// <summary>A geração em que o carro desenhado entra: a seguir à última marca da TABELA daquele tipo.
    /// Vai buscá-la à tabela e não aos desenhos, para redesenhar não fazer subir a ladeira sem fim.</summary>
    public static int NextMark(World w, int unitTypeId)
    {
        int last = 0;
        foreach (var m in w.EquipmentMarks.Values)
            if (m.UnitTypeId == unitTypeId && m.OwnerId == 0) last = Math.Max(last, m.Mark);
        return last + 1;
    }

    /// <summary>Os números deste desenho: o casco mais a soma das peças. É esta a marca que a fábrica passa
    /// a fazer e com que a divisão se bate — a mesma ficha para todos.</summary>
    public static EquipmentMarkDef Build(World w, TankDesign d)
    {
        var slots = Slots(w, d.Chassis);
        if (!w.TankChassis.TryGetValue(d.Chassis, out var b))
            b = new TankChassisDef(d.Chassis, d.Name, 3, 1f, 1f, 1f, "", "", 0, "lagarta", "");

        float cost = b.Cost, power = b.Power, wear = b.Wear;
        foreach (var m in Fitted(w, d, slots)) { cost += m.Cost; power += m.Power; wear += m.Wear; }

        return new EquipmentMarkDef(MarkId(d.Id), b.UnitTypeId, NextMark(w, b.UnitTypeId),
                                    d.Name.Length > 0 ? d.Name : b.Name, "",
                                    MathF.Max(0.2f, cost), MathF.Max(0.1f, power), Math.Clamp(wear, 0.3f, 2f),
                                    Note(w, d, b), Glyph(w, d, b), d.CountryId);
    }

    /// <summary>As peças mesmo montadas, pela ordem das ranhuras: salta ranhura vazia, peça desconhecida e
    /// peça que não é daquela ranhura (um save velho ou uma tabela mexida não podem dar números falsos).</summary>
    public static List<TankModuleDef> Fitted(World w, TankDesign d, List<string>? slots = null)
    {
        slots ??= Slots(w, d.Chassis);
        var got = new List<TankModuleDef>();
        for (int i = 0; i < slots.Count && i < d.Modules.Count; i++)
        {
            string id = d.Modules[i];
            if (id.Length == 0 || !w.TankModules.TryGetValue(id, out var m) || m.Slot != slots[i]) continue;
            got.Add(m);
        }
        return got;
    }

    /// <summary>A chapa do carro: a da peça mais cara que ele leva, e a do casco quando não leva nenhuma.
    /// Assim o carro de casa vê-se pelo que o faz diferente — na prancheta e na prateleira do armazém.</summary>
    private static string Glyph(World w, TankDesign d, TankChassisDef b)
    {
        var heavy = Fitted(w, d).Where(m => m.Glyph.Length > 0).OrderByDescending(m => m.Cost)
            .ThenBy(m => m.Sort).FirstOrDefault();
        return heavy?.Glyph ?? b.Glyph;
    }

    private static string Note(World w, TankDesign d, TankChassisDef b)
    {
        var parts = Fitted(w, d).Select(m => m.Name.ToLowerInvariant()).ToList();
        return parts.Count == 0
            ? $"Desenhado em casa sobre {b.Name.ToLowerInvariant()}, sem peça nenhuma montada."
            : $"Desenhado em casa sobre {b.Name.ToLowerInvariant()}: {string.Join(", ", parts)}.";
    }

    /// <summary>Põe (ou repõe) o carro na tabela de marcas do mundo. Chamado a criar, a redesenhar e a abrir
    /// um save — é o único sítio onde um desenho vira marca de material.</summary>
    public static void Register(World w, TankDesign d)
    {
        if (!w.TankDesigns.Contains(d)) w.TankDesigns.Add(d);
        w.EquipmentMarks[MarkId(d.Id)] = Build(w, d);
    }

    /// <summary>Tira o carro da tabela de marcas e da prancheta. O material já feito não desaparece: fica no
    /// armazém com a marca que tinha, e as contas dele voltam a cair na última geração da tabela.</summary>
    public static void Forget(World w, TankDesign d)
    {
        w.EquipmentMarks.Remove(MarkId(d.Id));
        w.TankDesigns.Remove(d);
    }

    /// <summary>Os carros de um país, do mais velho para o mais novo.</summary>
    public static List<TankDesign> Of(World w, int countryId) =>
        w.TankDesigns.Where(d => d.CountryId == countryId).OrderBy(d => d.Id).ToList();

    /// <summary>O carro que este país já desenhou para esta ladeira de material (null se ainda nenhum). A
    /// ladeira é uma só: cada país tem, quando muito, uma geração de casa por tipo de unidade.</summary>
    public static TankDesign? For(World w, int countryId, int unitTypeId) =>
        Of(w, countryId).FirstOrDefault(d => w.TankChassis.TryGetValue(d.Chassis, out var b)
                                             && b.UnitTypeId == unitTypeId);

    /// <summary>O número do desenho a seguir: nunca reaproveita um que já foi usado.</summary>
    public static int NextId(World w) => w.TankDesigns.Count == 0 ? 1 : w.TankDesigns.Max(d => d.Id) + 1;

    /// <summary>Experiência de exército que a assinatura custa: menos para mexer num carro que já existe.</summary>
    public static float Price(World w, bool editing) =>
        w.Rule(editing ? "tank_design_edit_xp" : "tank_design_xp", editing ? 14f : 35f);

    /// <summary>A força que o desenho tem de bater: a da melhor marca que a fábrica já sabe fazer, vezes a
    /// aresta da regra. Zero num mundo sem marcas — aí qualquer carro serve.</summary>
    public static float Bar(World w, int countryId, int unitTypeId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return 0f;
        float best = 0f;
        foreach (var m in w.EquipmentMarks.Values)
            if (m.UnitTypeId == unitTypeId && m.OwnerId == 0
                && (m.TechId.Length == 0 || c.Techs.Contains(m.TechId)))
                best = MathF.Max(best, m.Power);
        return best * w.Rule("tank_design_edge", 1f);
    }

    /// <summary>O que impede este carro de ser assinado, em palavras — null quando está pronto. É a mesma
    /// porta para o comando, para a prancheta e para a IA: a resposta tem de ser uma só.</summary>
    public static string? Check(World w, int countryId, string chassis, IReadOnlyList<string> modules,
                                int editing = 0)
    {
        if (!w.Countries.TryGetValue(countryId, out var c)) return "País desconhecido";
        if (!w.TankChassis.TryGetValue(chassis, out var b)) return "Casco desconhecido";
        if (!b.Designable) return $"{b.Name} não se desenha: vem feito da tabela";
        if (ChassisLocked(w, countryId, chassis) is { Length: > 0 } ctech)
            return $"{b.Name} precisa de {ctech}";
        var slots = Slots(w, chassis);
        if (modules.Count != slots.Count) return $"{b.Name} tem {slots.Count} ranhuras, não {modules.Count}";

        for (int i = 0; i < slots.Count; i++)
        {
            string id = modules[i];
            w.TankSlotDefs.TryGetValue(slots[i], out var slot);
            string slotName = slot?.Name ?? slots[i];
            if (id.Length == 0)
            {
                if (slot is not null && slot.Required) return $"Ranhura de {slotName.ToLowerInvariant()} vazia";
                continue;
            }
            if (!w.TankModules.TryGetValue(id, out var m)) return "Peça desconhecida";
            if (m.Slot != slots[i]) return $"{m.Name} não entra numa ranhura de {slotName.ToLowerInvariant()}";
            if (Locked(w, countryId, id) is { Length: > 0 } tech) return $"{m.Name} precisa de {tech}";
        }

        var mine = For(w, countryId, b.UnitTypeId);
        if (editing == 0)
        {
            if (mine is not null)
                return $"Já há um carro de casa para {w.Units.GetUnitType(b.UnitTypeId).Name.ToLowerInvariant()}: mexe nesse";
        }
        else
        {
            var edit = w.TankDesigns.FirstOrDefault(d => d.Id == editing && d.CountryId == countryId);
            if (edit is null) return "Esse desenho não é desta casa";
            if (mine is not null && mine.Id != editing)
                return $"Já há um carro de casa para {w.Units.GetUnitType(b.UnitTypeId).Name.ToLowerInvariant()}: mexe nesse";
        }

        // a aresta: o carro de casa é a geração SEGUINTE, por isso tem de bater o que a fábrica já faz
        float bar = Bar(w, countryId, b.UnitTypeId);
        float power = Build(w, new TankDesign { Id = editing, CountryId = countryId, Chassis = chassis,
                                                Modules = modules.ToList() }).Power;
        if (bar > 0f && power < bar - 0.0001f)
            return $"Não bate o que a fábrica já faz: {power:0.00} contra {bar:0.00}";

        float price = Price(w, editing != 0);
        if (c.ArmyXp < price) return $"Falta experiência: a prancheta pede {price:0} de exército";
        return null;
    }

    /// <summary>Os conselhos à margem, do Core e não do ecrã: o que este carro faz bem, o que não faz de
    /// todo e o que está a pagar sem usar. Bad a true é o que impede a assinatura.</summary>
    public static List<(string Text, bool Bad)> Advice(World w, int countryId, string chassis,
                                                       IReadOnlyList<string> modules)
    {
        var notes = new List<(string, bool)>();
        var slots = Slots(w, chassis);
        if (slots.Count == 0 || !w.TankChassis.TryGetValue(chassis, out var b)) return notes;

        var sheet = Build(w, new TankDesign { CountryId = countryId, Chassis = chassis, Modules = modules.ToList() });
        var fitted = Fitted(w, new TankDesign { Chassis = chassis, Modules = modules.ToList() }, slots);

        for (int i = 0; i < slots.Count && i < modules.Count; i++)
        {
            if (modules[i].Length > 0) continue;
            if (w.TankSlotDefs.TryGetValue(slots[i], out var s) && s.Required)
                notes.Add(($"Sem {s.Name.ToLowerInvariant()} isto é um tractor com chapa.", true));
        }

        float bar = Bar(w, countryId, b.UnitTypeId);
        if (bar > 0f)
            notes.Add(sheet.Power < bar - 0.0001f
                ? ($"Bate {sheet.Power:0.00} e a fábrica já faz {bar:0.00}: assim não se assina.", true)
                : ($"Bate {sheet.Power:0.00} contra os {bar:0.00} que a fábrica já faz.", false));

        int empty = 0;
        for (int i = 0; i < slots.Count; i++) if (i >= modules.Count || modules[i].Length == 0) empty++;
        if (empty > 0)
            notes.Add(($"{empty} ranhura{(empty == 1 ? "" : "s")} por encher: aço a menos onde cabia guerra.", false));

        if (sheet.Cost > 2.5f)
            notes.Add(($"Custa {sheet.Cost:0.00} conjuntos por um: carro de desfile, se a fábrica não crescer.", false));
        if (sheet.Wear > 1.02f)
            notes.Add(("Gasta-se mais depressa do que o material de origem: a frente vai pedi-lo todos os dias.", false));
        else if (sheet.Wear < 0.85f)
            notes.Add(("Aguenta guerra: volta da frente com menos buracos do que o que lá está.", false));
        if (fitted.Count > 0 && sheet.Power / MathF.Max(0.2f, sheet.Cost) > 1.1f)
            notes.Add(("Bom preço pelo que bate: dá para armar exército, não uma companhia.", false));
        notes.Add(($"Entra como {Marks.Roman(sheet.Mark)} de {w.Units.GetUnitType(b.UnitTypeId).Name.ToLowerInvariant()}.", false));
        return notes;
    }

    /// <summary>A ficha em uma linha, para a prova headless e para a lista de carros.</summary>
    public static string Short(World w, EquipmentMarkDef d) =>
        $"{d.Name} ({Marks.Roman(d.Mark)}): bate ×{d.Power:0.00}, custa ×{d.Cost:0.00}, gasta ×{d.Wear:0.00}";
}
