using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>O chão do céu: campos de aviação, lotação e alcance (HoI4: air base level e o raio da asa).
///
/// Até aqui a aviação não vivia em sítio nenhum. As asas eram um número nacional (Country.AirPower) que
/// aparecia em qualquer céu do mundo desde que a província fizesse fronteira com terra nossa — um país
/// europeu punha a força aérea inteira sobre a Coreia no dia em que tomasse uma província ao lado. Não
/// havia campos, não havia lotação, não havia distância: era o último sítio do jogo onde a geografia não
/// contava para nada.
///
/// Agora uma asa dorme num campo e é o campo que manda:
///  • LOTAÇÃO — cada província nossa assenta `air_base_free` asas em pista improvisada (é a aviação de
///    sempre, e por isso nenhum mundo antigo fica em terra); um campo de aviação (building.air_slots)
///    acrescenta as suas por nível. Somadas, são as camas que o país tem naquele pedaço de mundo;
///  • ALCANCE — cada modelo tem o seu raio (plane_class.range_km) e a pista comprida deixa levantar com
///    mais depósitos (building.air_range por nível). A asa só chega ao céu que estiver dentro do raio do
///    campo onde dorme, medido em km REAIS (World.Km, pela lat/lon) e não em unidades de mapa.
///
/// Uma missão pode dormir em vários campos — são todos ali ao lado — por isso o que interessa é o total de
/// camas ao alcance daquele céu. As camas dão-se por ordem de missão e sempre do campo mais perto para o
/// mais longe: a conta é a mesma em todas as máquinas.
///
/// E há campos que flutuam: o PORTA-AVIÕES (ship_class.deck) é um campo de aviação a andar pelo mar. As
/// asas que ele leva têm de caber num convés (plane_class.deck: o caça de superioridade é grande de mais)
/// e levantam com o alcance curto de `air_carrier_range` — é o casco que vai perto, não o avião. É por
/// aqui que a aviação chega a mar onde não há terra nossa nenhuma, e é a razão de haver porta-aviões.
///
/// O que isto muda no jogo: massar aviação sobre uma frente longe de casa passa a ser uma obra a fazer
/// antes da guerra, e não uma decisão de um segundo. É a razão de existir o campo de aviação.
///
/// Estado derivado: não guarda nada de seu, não entra no save e não é ISystem.</summary>
public static class AirBases
{
    /// <summary>Nível de campo de aviação de uma província (0 = nenhum, só pista improvisada).</summary>
    public static int Level(World w, Region r)
    {
        int lvl = 0;
        foreach (var (id, n) in r.Buildings)
            if (n > 0 && w.BuildingDefs.TryGetValue(id, out var d) && d.IsAirfield) lvl += n;
        return lvl;
    }

    /// <summary>Asas que esta província assenta para quem a controla: a pista improvisada de sempre mais o
    /// que os campos lá levantados acrescentam.</summary>
    public static float Slots(World w, Region r)
    {
        float slots = w.Rule("air_base_free", 4f);
        foreach (var (id, n) in r.Buildings)
            if (n > 0 && w.BuildingDefs.TryGetValue(id, out var d) && d.IsAirfield) slots += n * d.AirSlots;
        return slots;
    }

    /// <summary>Km que os campos desta província acrescentam ao raio de quem lá dorme.</summary>
    public static float Extra(World w, Region r)
    {
        float km = 0f;
        foreach (var (id, n) in r.Buildings)
            if (n > 0 && w.BuildingDefs.TryGetValue(id, out var d) && d.IsAirfield) km += n * d.AirRange;
        return km;
    }

    /// <summary>Raio de uma composição de aviões: o do modelo que chega menos longe — uma formação não se
    /// parte a meio do caminho. Sem modelos (saves antigos, mundos de teste) vale air_range_default.</summary>
    public static float Range(World w, IReadOnlyDictionary<string, float> squadron)
    {
        float fallback = w.Rule("air_range_default", 2000f);
        float min = float.MaxValue;
        foreach (var (cls, n) in squadron)
        {
            if (n <= 0.0001f) continue;
            float km = cls.Length > 0 && w.PlaneClasses.TryGetValue(cls, out var d) && d.RangeKm > 0f ? d.RangeKm : fallback;
            if (km < min) min = km;
        }
        return min == float.MaxValue ? fallback : min;
    }

    /// <summary>Campos deste país (províncias que controla), do mais perto do alvo para o mais longe.
    /// Empates pelo id, para o mundo não depender de sementes.</summary>
    public static List<Region> Fields(World w, int countryId, int targetId)
    {
        var list = w.Regions.Values.Where(r => r.ControllerId == countryId).ToList();
        list.Sort((a, b) =>
        {
            int c = w.Km(a.Id, targetId).CompareTo(w.Km(b.Id, targetId));
            return c != 0 ? c : a.Id.CompareTo(b.Id);
        });
        return list;
    }

    /// <summary>Conveses deste país por mar onde tem esquadra: as asas que os porta-aviões destacados
    /// assentam ali. Um casco só conta enquanto estiver no mar — chamado ao porto, leva o campo com ele.</summary>
    public static Dictionary<int, float> Decks(World w, int countryId)
    {
        var decks = new Dictionary<int, float>();
        foreach (var m in w.NavalMissions)
        {
            if (m.CountryId != countryId) continue;
            float slots = Navy.Decks(w, m.Squadron);
            if (slots > 0f) decks[m.RegionId] = decks.GetValueOrDefault(m.RegionId) + slots;
        }
        return decks;
    }

    /// <summary>Uma cama à espera: onde é (região), quantas asas assenta, até onde as deixa chegar e se é
    /// convés (que só recebe aviões de convés). Terra e mar contam-se no mesmo saco porque para a asa a
    /// pergunta é a mesma: cabe-me aqui e chego lá?</summary>
    public readonly record struct Berth(Region Field, float Slots, float Reach, bool Carrier);

    /// <summary>Camas deste país para um céu, do mais perto para o mais longe (o convés fica atrás da terra
    /// quando empatam, que a terra é mais segura). O <paramref name="reach"/> é o raio dos aviões que vão
    /// levantar: em terra soma-se-lhe a pista comprida, no convés trava-o o alcance curto de bordo.</summary>
    public static List<Berth> Berths(World w, int countryId, int targetId, float reach)
    {
        float sea = w.Rule("air_carrier_range", 600f);
        var decks = Decks(w, countryId);
        var list = new List<Berth>();
        foreach (var r in w.Regions.Values)
        {
            if (r.ControllerId == countryId) list.Add(new Berth(r, Slots(w, r), reach + Extra(w, r), false));
            if (decks.TryGetValue(r.Id, out var deck) && deck > 0f)
                list.Add(new Berth(r, deck, MathF.Min(reach, sea), true));
        }
        list.Sort((a, b) =>
        {
            int c = w.Km(a.Field.Id, targetId).CompareTo(w.Km(b.Field.Id, targetId));
            if (c != 0) return c;
            c = a.Carrier.CompareTo(b.Carrier);
            return c != 0 ? c : a.Field.Id.CompareTo(b.Field.Id);
        });
        return list;
    }

    /// <summary>Asas desta composição que cabem num convés (plane_class.deck). O avião sem modelo não cabe:
    /// um convés não é uma pista, e a marinha antiga não tinha porta-aviões nenhum para o receber.</summary>
    public static float DeckWings(World w, IReadOnlyDictionary<string, float> squadron)
    {
        float sum = 0f;
        foreach (var (cls, n) in squadron)
            if (n > 0f && cls.Length > 0 && w.PlaneClasses.TryGetValue(cls, out var d) && d.Deck) sum += n;
        return sum;
    }

    /// <summary>Quem dorme onde: para cada missão deste país, os campos que a assentam e quantas asas
    /// ficam em cada um. As missões servem-se por ordem de província e cada uma enche do campo mais perto
    /// para o mais longe — as que não arranjam cama ficam em terra (o resto do dicionário fica curto).</summary>
    public static Dictionary<AirMission, List<(int RegionId, float Wings)>> Beds(World w, int countryId) =>
        Beds(w, countryId, out _);

    /// <summary>O mesmo, e ainda o que sobrou de cada campo (chave: região e se é convés). Um campo sem
    /// entrada é um campo onde ninguém dormiu — vale a lotação inteira.</summary>
    public static Dictionary<AirMission, List<(int RegionId, float Wings)>> Beds(
        World w, int countryId, out Dictionary<(int Region, bool Carrier), float> left)
    {
        var beds = new Dictionary<AirMission, List<(int, float)>>();
        left = new Dictionary<(int Region, bool Carrier), float>();      // camas que cada campo ainda tem
        foreach (var m in w.AirMissions.Where(x => x.CountryId == countryId).OrderBy(x => x.RegionId).ToList())
        {
            var mine = new List<(int, float)>();
            float need = m.Wings, reach = Range(w, m.Squadron), deckLeft = DeckWings(w, m.Squadron);
            foreach (var b in Berths(w, countryId, m.RegionId, reach))
            {
                if (need <= 0.0001f) break;
                if (w.Km(b.Field.Id, m.RegionId) > b.Reach) continue;           // longe de mais para esta asa
                var key = (b.Field.Id, b.Carrier);
                float room = left.TryGetValue(key, out var v) ? v : b.Slots;
                // no convés só assenta quem cabe num convés: o resto da asa fica à espera de terra
                float want = b.Carrier ? MathF.Min(need, deckLeft) : need;
                if (room <= 0.0001f || want <= 0.0001f) { left[key] = MathF.Max(0f, room); continue; }
                float take = MathF.Min(room, want);
                left[key] = room - take;
                need -= take;
                deckLeft = MathF.Min(deckLeft - (b.Carrier ? take : 0f), need);
                mine.Add((b.Field.Id, take));
            }
            beds[m] = mine;
        }
        return beds;
    }

    /// <summary>Asas desta missão que têm cama num campo ao alcance. O que passar disto está em terra.</summary>
    public static float Seated(World w, AirMission m)
    {
        var beds = Beds(w, m.CountryId);
        return beds.TryGetValue(m, out var mine) ? mine.Sum(b => b.Wings) : 0f;
    }

    /// <summary>O campo principal desta missão (onde dorme mais gente), para a ficha e para o mapa.</summary>
    public static int? Home(World w, AirMission m)
    {
        var beds = Beds(w, m.CountryId);
        if (!beds.TryGetValue(m, out var mine) || mine.Count == 0) return null;
        return mine.OrderByDescending(b => b.Wings).ThenBy(b => b.RegionId).First().RegionId;
    }

    /// <summary>Camas livres deste país para um céu, contando só os campos que lá chegam com este raio. O
    /// <paramref name="deckWings"/> trava o que os conveses podem prometer: de nada vale um porta-aviões
    /// vazio de camas se os aviões que iam levantar não cabem lá. É o que o painel mostra por baixo do
    /// botão e o que a IA não pode ultrapassar.</summary>
    public static float Room(World w, int countryId, int targetId, float reach, float deckWings = float.MaxValue)
    {
        Beds(w, countryId, out var left);
        float free = 0f, deck = deckWings;
        foreach (var b in Berths(w, countryId, targetId, reach))
        {
            if (w.Km(b.Field.Id, targetId) > b.Reach) continue;
            var key = (b.Field.Id, b.Carrier);
            float room = MathF.Max(0f, left.TryGetValue(key, out var v) ? v : b.Slots);
            if (b.Carrier) { room = MathF.Min(room, deck); deck -= room; }
            free += room;
        }
        return free;
    }

    /// <summary>Há campo nosso que chegue a este céu com este raio? (a pista comprida conta, e o convés
    /// também: um porta-aviões destacado põe asas em mar onde não há terra nossa nenhuma).</summary>
    public static bool Covers(World w, int countryId, int targetId, float reach) =>
        Berths(w, countryId, targetId, reach).Any(b => w.Km(b.Field.Id, targetId) <= b.Reach);

    /// <summary>Camas livres para o que este país costuma mandar a esta tarefa (o raio e o feitio dos
    /// aviões que levantariam hoje). É a conta que o comando faz antes de deixar destacar mais asas.</summary>
    public static float Room(World w, int countryId, int targetId, string effect, float wings)
    {
        var take = Air.Pick(w, countryId, effect, wings);
        return Room(w, countryId, targetId, Range(w, take), DeckWings(w, take));
    }

    /// <summary>Campo mais perto deste céu que ainda tem cama, e a que distância fica — para explicar a
    /// recusa por palavras: "o campo mais perto fica a 3 200 km e a asa só chega a 1 300".</summary>
    public static (Region? Field, float Km) Nearest(World w, int countryId, int targetId)
    {
        var berths = Berths(w, countryId, targetId, 0f);   // o raio não conta para saber qual é o mais perto
        return berths.Count == 0 ? (null, float.MaxValue) : (berths[0].Field, w.Km(berths[0].Field.Id, targetId));
    }

    /// <summary>O chão do céu deste país numa linha: campos levantados, camas ocupadas e a asa que ficou em
    /// terra. Serve a ficha do hangar e a prova headless.</summary>
    public static string Short(World w, int countryId)
    {
        var beds = Beds(w, countryId);
        float seated = beds.Sum(b => b.Value.Sum(x => x.Wings));
        float flying = w.AirMissions.Where(m => m.CountryId == countryId).Sum(m => m.Wings);
        int fields = w.Regions.Values.Count(r => r.ControllerId == countryId && Level(w, r) > 0);
        var decks = Decks(w, countryId);
        float slots = w.Regions.Values.Where(r => r.ControllerId == countryId).Sum(r => Slots(w, r))
                    + decks.Values.Sum();
        string mar = decks.Count == 0 ? ""
                   : $", {decks.Count} mar{(decks.Count == 1 ? "" : "es")} com convés ({decks.Values.Sum():0.#} camas a flutuar)";
        string terra = flying - seated > 0.05f ? $", {flying - seated:0.#} asas em terra por falta de campo" : "";
        return $"{fields} campo{(fields == 1 ? "" : "s")} de aviação{mar}, {seated:0.#}/{slots:0} camas ocupadas{terra}";
    }
}
