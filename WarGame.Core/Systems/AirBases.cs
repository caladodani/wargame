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

    /// <summary>Quem dorme onde: para cada missão deste país, os campos que a assentam e quantas asas
    /// ficam em cada um. As missões servem-se por ordem de província e cada uma enche do campo mais perto
    /// para o mais longe — as que não arranjam cama ficam em terra (o resto do dicionário fica curto).</summary>
    public static Dictionary<AirMission, List<(int RegionId, float Wings)>> Beds(World w, int countryId)
    {
        var beds = new Dictionary<AirMission, List<(int, float)>>();
        var left = new Dictionary<int, float>();                       // camas que cada província ainda tem
        foreach (var m in w.AirMissions.Where(x => x.CountryId == countryId).OrderBy(x => x.RegionId).ToList())
        {
            var mine = new List<(int, float)>();
            float need = m.Wings, reach = Range(w, m.Squadron);
            foreach (var f in Fields(w, countryId, m.RegionId))
            {
                if (need <= 0.0001f) break;
                if (w.Km(f.Id, m.RegionId) > reach + Extra(w, f)) continue;     // longe de mais para esta asa
                float room = left.TryGetValue(f.Id, out var v) ? v : Slots(w, f);
                if (room <= 0.0001f) { left[f.Id] = 0f; continue; }
                float take = MathF.Min(room, need);
                left[f.Id] = room - take;
                need -= take;
                mine.Add((f.Id, take));
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

    /// <summary>Camas livres deste país para um céu, contando só os campos que lá chegam com este raio.
    /// É o que o painel mostra por baixo do botão e o que a IA não pode ultrapassar.</summary>
    public static float Room(World w, int countryId, int targetId, float reach)
    {
        var used = new Dictionary<int, float>();
        foreach (var (_, mine) in Beds(w, countryId))
            foreach (var (rid, n) in mine) used[rid] = used.GetValueOrDefault(rid) + n;
        float free = 0f;
        foreach (var f in Fields(w, countryId, targetId))
            if (w.Km(f.Id, targetId) <= reach + Extra(w, f))
                free += MathF.Max(0f, Slots(w, f) - used.GetValueOrDefault(f.Id));
        return free;
    }

    /// <summary>Há campo nosso que chegue a este céu com este raio? (a pista comprida conta: um campo mais
    /// longe pode cobrir o que o campo ao lado não cobre).</summary>
    public static bool Covers(World w, int countryId, int targetId, float reach) =>
        Fields(w, countryId, targetId).Any(f => w.Km(f.Id, targetId) <= reach + Extra(w, f));

    /// <summary>Camas livres para o que este país costuma mandar a esta tarefa (o raio dos aviões que
    /// levantariam hoje). É a conta que o comando faz antes de deixar destacar mais asas.</summary>
    public static float Room(World w, int countryId, int targetId, string effect, float wings) =>
        Room(w, countryId, targetId, Range(w, Air.Pick(w, countryId, effect, wings)));

    /// <summary>Campo mais perto deste céu que ainda tem cama, e a que distância fica — para explicar a
    /// recusa por palavras: "o campo mais perto fica a 3 200 km e a asa só chega a 1 300".</summary>
    public static (Region? Field, float Km) Nearest(World w, int countryId, int targetId)
    {
        var fields = Fields(w, countryId, targetId);
        return fields.Count == 0 ? (null, float.MaxValue) : (fields[0], w.Km(fields[0].Id, targetId));
    }

    /// <summary>O chão do céu deste país numa linha: campos levantados, camas ocupadas e a asa que ficou em
    /// terra. Serve a ficha do hangar e a prova headless.</summary>
    public static string Short(World w, int countryId)
    {
        var beds = Beds(w, countryId);
        float seated = beds.Sum(b => b.Value.Sum(x => x.Wings));
        float flying = w.AirMissions.Where(m => m.CountryId == countryId).Sum(m => m.Wings);
        int fields = w.Regions.Values.Count(r => r.ControllerId == countryId && Level(w, r) > 0);
        float slots = w.Regions.Values.Where(r => r.ControllerId == countryId).Sum(r => Slots(w, r));
        string terra = flying - seated > 0.05f ? $", {flying - seated:0.#} asas em terra por falta de campo" : "";
        return $"{fields} campo{(fields == 1 ? "" : "s")} de aviação, {seated:0.#}/{slots:0} camas ocupadas{terra}";
    }
}
