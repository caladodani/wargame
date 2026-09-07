using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Teatros de operações (HoI4: theatres/fronts). Até aqui uma guerra era uma linha no painel — "contra
/// a Argentina, 12 regiões tomadas" — e o jogador não tinha maneira nenhuma de ver que aquela guerra eram na
/// verdade duas frentes, uma no norte guarnecida a dobrar e outra no sul aberta de par em par.
///
/// Um teatro é um troço de linha de contacto: as nossas regiões que fazem fronteira com terra controlada por um
/// inimigo, agrupadas por vizinhança. Duas regiões de contacto coladas uma à outra são a mesma frente; separadas
/// por terra tranquila, são frentes diferentes, cada uma com o seu nome (o da maior região do troço), a sua
/// guarnição e o seu buraco.
///
/// Cada teatro traz três números: a <b>cobertura</b> (divisões que lá temos contra as que a frente pede —
/// theatre_need_per_region por região de contacto), os <b>buracos</b> (regiões de contacto sem uma única divisão,
/// por onde o inimigo entra sem disparar) e o <b>avanço</b> contra aquele inimigo (a fatia da terra dele que já
/// controlamos). Nada disto se guarda: é tudo lido do estado do dia, como as rotas dos comboios.
///
/// O sistema em si só serve para uma coisa: dar pela passagem de cada degrau de avanço (theatre_milestone) e
/// publicar FrontAdvanced, para a crónica escrever que a frente rompeu. Os degraus vivem em memória — um save
/// recarregado volta a anunciar o degrau em que já ia, e mais vale isso do que uma tabela de save para um
/// aviso.</summary>
public sealed class TheatreSystem : ISystem
{
    public string Name => "Theatres";

    private World? _bound;
    /// <summary>Degrau de avanço já anunciado por par (país, inimigo). Recuar baixa-o outra vez: uma frente
    /// que se perde e se volta a ganhar merece ser contada de novo.</summary>
    private readonly Dictionary<(int Mine, int Foe), int> _step = new();

    public void Tick(World w)
    {
        if (!ReferenceEquals(_bound, w)) { _bound = w; _step.Clear(); }
        float step = MathF.Max(0.05f, w.Rule("theatre_milestone", 0.25f));

        foreach (var c in w.Countries.Values.OrderBy(c => c.Id))
        {
            if (c.Capitulated) continue;
            foreach (int foe in c.AtWarWith.OrderBy(x => x).ToList())
            {
                float progress = Progress(w, c.Id, foe);
                int reached = (int)MathF.Floor(progress / step);
                int had = _step.GetValueOrDefault((c.Id, foe));
                if (reached > had && reached > 0)
                    w.Events.Publish(new FrontAdvanced(c.Id, foe, progress, Of(w, c.Id).Count(t => t.FoeId == foe)));
                if (reached != had) _step[(c.Id, foe)] = reached;
            }
        }
        // pares que já não estão em guerra não têm degrau nenhum para guardar
        foreach (var k in _step.Keys.Where(k => !w.Countries.TryGetValue(k.Mine, out var c) || !c.AtWarWith.Contains(k.Foe)).ToList())
            _step.Remove(k);
    }

    /// <summary>Fatia da terra do inimigo que este país já controla: 0 no dia da declaração, 1 quando não lhe
    /// resta uma região. Sem terra nenhuma no nome dele, dá-se a guerra por ganha.</summary>
    public static float Progress(World w, int countryId, int foeId)
    {
        int own = 0, held = 0;
        foreach (var r in w.Regions.Values)
        {
            if (r.OwnerId != foeId) continue;
            own++;
            if (r.ControllerId == countryId) held++;
        }
        return own == 0 ? 1f : (float)held / own;
    }

    /// <summary>Os teatros deste país, dos maiores para os mais pequenos. Um por troço de linha de contacto e
    /// por inimigo: uma região encostada a dois inimigos está em duas frentes, porque são duas guerras.</summary>
    public static List<Theatre> Of(World w, int countryId)
    {
        var list = new List<Theatre>();
        if (!w.Countries.TryGetValue(countryId, out var me) || me.AtWarWith.Count == 0) return list;

        float need = MathF.Max(0.1f, w.Rule("theatre_need_per_region", 1.5f));
        // divisões por região, contadas uma vez: numa guerra grande a linha de contacto tem centenas de troços
        var garrison = new Dictionary<(int Region, int Country), int>();
        foreach (var d in w.Divisions.Values)
            garrison[(d.RegionId, d.CountryId)] = garrison.GetValueOrDefault((d.RegionId, d.CountryId)) + 1;

        foreach (int foe in me.AtWarWith.OrderBy(x => x))
        {
            // regiões nossas coladas a terra que este inimigo controla: a linha de contacto desta guerra
            var contact = w.Regions.Values
                .Where(r => r.ControllerId == countryId
                            && r.Neighbours.Any(n => w.Regions.TryGetValue(n, out var nb) && nb.ControllerId == foe))
                .OrderBy(r => r.Id).ToList();
            if (contact.Count == 0) continue;

            float progress = Progress(w, countryId, foe);
            var pending = contact.Select(r => r.Id).ToHashSet();
            foreach (var seed in contact)
            {
                if (!pending.Remove(seed.Id)) continue;
                // troço: tudo o que se alcança de vizinho em vizinho sem sair da linha de contacto
                var group = new List<Region> { seed };
                var queue = new Queue<Region>(); queue.Enqueue(seed);
                while (queue.Count > 0)
                    foreach (int n in queue.Dequeue().Neighbours.OrderBy(x => x))
                        if (pending.Remove(n) && w.Regions.TryGetValue(n, out var nb)) { group.Add(nb); queue.Enqueue(nb); }

                var ids = group.Select(r => r.Id).OrderBy(x => x).ToList();
                int mine = ids.Sum(id => garrison.GetValueOrDefault((id, countryId)));
                var facing = group.SelectMany(r => r.Neighbours)
                    .Where(n => w.Regions.TryGetValue(n, out var nb) && nb.ControllerId == foe).Distinct().ToHashSet();
                int theirs = facing.Sum(id => garrison.GetValueOrDefault((id, foe)));
                int holes = ids.Count(id => garrison.GetValueOrDefault((id, countryId)) == 0);
                float want = ids.Count * need;

                // identidade do teatro p/ um grupo se ancorar nele: uma região inimiga representativa deste
                // troço (a de id mais baixo, estável entre chamadas enquanto o território não muda de mão).
                int facingId = facing.Count > 0 ? facing.Min() : ids[0];
                list.Add(new Theatre(foe, Title(group), ids, mine, theirs, want,
                                     want <= 0f ? 1f : MathF.Min(1f, mine / want), progress, holes, facingId));
            }
        }
        return list.OrderByDescending(t => t.RegionIds.Count).ThenBy(t => t.FoeId).ThenBy(t => t.RegionIds[0]).ToList();
    }

    /// <summary>Nome do teatro: o da região com mais gente do troço, que é a que o jogador reconhece no mapa.</summary>
    private static string Title(List<Region> group)
    {
        var head = group.OrderByDescending(r => r.Population).ThenBy(r => r.Id).First();
        return group.Count == 1 ? $"Frente de {head.Name}" : $"Frente de {head.Name} (+{group.Count - 1})";
    }
}

/// <summary>Um troço de frente contra um inimigo: as regiões de contacto, quem lá está de cada lado, quanto a
/// frente pede, quanto está guarnecida, quanto já avançámos contra aquele país e quantos buracos tem.
/// FacingId identifica o troço p/ um grupo se ancorar nele (SetArmyGroupFrontCommand.RegionId).</summary>
public readonly record struct Theatre(int FoeId, string Name, IReadOnlyList<int> RegionIds, int Divisions,
                                      int FoeDivisions, float Need, float Coverage, float Progress, int Holes,
                                      int FacingId);
