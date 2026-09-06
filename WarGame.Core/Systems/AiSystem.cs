using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Países sem jogador (HoI4): produzem continuamente, guarnecem a fronteira e atacam a província
/// inimiga mais fraca quando têm superioridade. Toda a acção passa por ICommand (validado antes de executar).
/// Corre de ai_period_days em ai_period_days. Regras: ai_period_days, ai_attack_ratio, ai_max_queue,
/// ai_min_org, ai_heavy_every.</summary>
public sealed class AiSystem : ISystem
{
    public string Name => "AI";

    public void Tick(World w)
    {
        int period = Math.Max(1, (int)w.Rule("ai_period_days", 3));
        if (w.Clock.Day % period != 0) return;

        // Índices construídos uma vez por tick (~250 países, ~3000 regiões): país→divisões, região→país→CanFight.
        var divsByCountry = new Dictionary<int, List<Division>>();
        var fighters = new Dictionary<int, Dictionary<int, int>>();
        foreach (var d in w.Divisions.Values)
        {
            if (!divsByCountry.TryGetValue(d.CountryId, out var list)) divsByCountry[d.CountryId] = list = new();
            list.Add(d);
            if (!d.CanFight) continue;
            if (!fighters.TryGetValue(d.RegionId, out var byCountry)) fighters[d.RegionId] = byCountry = new();
            byCountry[d.CountryId] = byCountry.GetValueOrDefault(d.CountryId) + 1;
        }
        var regionsByController = new Dictionary<int, List<Region>>();
        foreach (var r in w.Regions.Values)
        {
            if (!regionsByController.TryGetValue(r.ControllerId, out var list)) regionsByController[r.ControllerId] = list = new();
            list.Add(r);
        }
        var inBattle = new HashSet<int>(w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders)));

        FactionInvites(w);

        foreach (var c in w.Countries.Values)
        {
            if (c.IsPlayer) continue;   // o jogador nunca é mexido pela IA
            if (c.Capitulated) continue;   // capitulou: sem regiões nem exército, nada a fazer
            var divs = divsByCountry.GetValueOrDefault(c.Id);
            Research(w, c);
            if (divs is null && c.Money <= 0f) continue;
            Produce(w, c, divs?.Count ?? 0);
            Build(w, c, regionsByController.GetValueOrDefault(c.Id));
            Laws(w, c);
            Aid(w, c);
            Spy(w, c, divsByCountry);
            Naps(w, c, regionsByController.GetValueOrDefault(c.Id), divsByCountry);
            if (c.AtWarWith.Count == 0 && divs is not null) WarGoal(w, c, divs.Count, divsByCountry, regionsByController.GetValueOrDefault(c.Id));
            // Sem guerra não há nada a fazer por terra. TODO: "war goals" (declarar guerra a vizinhos fracos).
            if (c.AtWarWith.Count == 0 || divs is null) continue;
            Retreats(w, c);
            Peace(w, c, divsByCountry);
            Fight(w, c, divs, regionsByController.GetValueOrDefault(c.Id), fighters, inBattle);
        }
    }

    /// <summary>Guerra parada (sem progresso há peace_stale_days) em que estamos mais fracos:
    /// oferece paz branca — o comando aceita porque a guerra está parada. O jogador nunca é alvo
    /// (a paz far-se-ia sem o consentimento dele); a ele cabe oferecer pelo painel.</summary>
    private static void Peace(World w, Country c, Dictionary<int, List<Division>> divsByCountry)
    {
        foreach (int e in c.AtWarWith.ToList())
        {
            if (!w.Countries.TryGetValue(e, out var t) || t.Capitulated || t.IsPlayer) continue;
            var info = w.Wars.GetValueOrDefault(World.WarKey(c.Id, e));
            if (info is null || w.Clock.Day - Math.Max(info.StartDay, info.LastProgressDay) < w.Rule("peace_stale_days", 60f)) continue;
            int mine = divsByCountry.GetValueOrDefault(c.Id)?.Count ?? 0;
            int theirs = divsByCountry.GetValueOrDefault(e)?.Count ?? 0;
            // fecha se está a perder (corta perdas) ou se ocupa território do inimigo (uti
            // possidetis: a paz branca anexa o que controla — consolida os ganhos)
            bool holdsTheirLand = w.Regions.Values.Any(r => r.OwnerId == e && r.ControllerId == c.Id);
            if (mine >= theirs && !holdsTheirLand) continue;
            var cmd = new Commands.OfferPeaceCommand(c.Id, e);
            if (cmd.Validate(w) is null) cmd.Execute(w);
        }
    }

    /// <summary>Em guerra, segura as outras fronteiras: propõe não-agressão a um vizinho neutro
    /// por tick (sem guerra, facção ou pacto connosco) enquanto houver dinheiro acima de
    /// ai_nap_reserve. O alvo decide pela lógica do comando; o jogador nunca é alvo (sem UI de oferta).</summary>
    private static void Naps(World w, Country c, List<Region>? myRegions, Dictionary<int, List<Division>> divsByCountry)
    {
        if (c.AtWarWith.Count == 0 || myRegions is null) return;
        if (c.Money < w.Rule("nap_cost", 20f) + w.Rule("ai_nap_reserve", 100f)) return;
        var seen = new HashSet<int>();
        foreach (var r in myRegions)
            foreach (var n in r.Neighbours)
            {
                int t = w.Regions[n].ControllerId;
                if (t == c.Id || !seen.Add(t)) continue;
                if (!w.Countries.TryGetValue(t, out var tc) || tc.Capitulated || tc.IsPlayer) continue;
                if (w.AreAtWar(c.Id, t) || w.SameFaction(c.Id, t) || w.HasPact(c.Id, t)) continue;
                // só vale a pena se o alvo tende a aceitar: mais fraco ou com inimigo comum
                int mine = divsByCountry.GetValueOrDefault(c.Id)?.Count ?? 0;
                int theirs = divsByCountry.GetValueOrDefault(t)?.Count ?? 0;
                if (theirs >= mine && !tc.AtWarWith.Any(c.AtWarWith.Contains)) continue;
                var cmd = new Commands.ProposeNonAggressionCommand(c.Id, t);
                if (cmd.Validate(w) is null) { cmd.Execute(w); return; }
            }
    }

    /// <summary>Espionagem: em guerra e com dinheiro acima de ai_spy_reserve, lança a operação
    /// mais barata que ainda não corre contra o inimigo com mais divisões.</summary>
    private static void Spy(World w, Country c, Dictionary<int, List<Division>> divsByCountry)
    {
        if (c.AtWarWith.Count == 0 || w.SpyOps.Count == 0) return;
        if (c.Money <= w.Rule("ai_spy_reserve", 200f)) return;
        int target = -1, best = -1;
        foreach (var e in c.AtWarWith)
        {
            if (w.ActiveSpyOps.Any(o => o.CountryId == c.Id && o.TargetCountryId == e)) continue;
            int n = divsByCountry.GetValueOrDefault(e)?.Count ?? 0;
            if (n > best) { best = n; target = e; }
        }
        if (target < 0) return;
        // sob espionagem do alvo → contra-espionagem primeiro; senão a mais barata ofensiva
        bool spiedOn = w.ActiveSpyOps.Any(o => o.CountryId == target && o.TargetCountryId == c.Id);
        var op = (spiedOn ? w.SpyOps.Values.Where(o => o.Effect == "purge_spies") : Enumerable.Empty<SpyOp>())
            .Concat(w.SpyOps.Values.Where(o => o.Effect != "purge_spies").OrderBy(o => o.Cost))
            .First();
        var cmd = new Commands.StartSpyOpCommand(c.Id, target, op.Id);
        if (cmd.Validate(w) is null) cmd.Execute(w);
    }

    /// <summary>Apoio financeiro: acima de ai_aid_reserve envia ai_aid_share do excedente ao aliado
    /// de facção em guerra mais pobre (se estiver mais pobre que o próprio).</summary>
    private static void Aid(World w, Country c)
    {
        float reserve = w.Rule("ai_aid_reserve", 300f);
        if (c.Money <= reserve) return;
        Country? poorest = null;
        foreach (var a in w.Allies(c.Id))
            if (w.Countries.TryGetValue(a, out var ac) && !ac.Capitulated && ac.AtWarWith.Count > 0
                && ac.Money < c.Money && (poorest is null || ac.Money < poorest.Money)) poorest = ac;
        if (poorest is null) return;
        float amount = (c.Money - reserve) * w.Rule("ai_aid_share", 0.25f);
        var cmd = new TransferMoneyCommand(c.Id, poorest.Id, amount);
        if (cmd.Validate(w) is null) cmd.Execute(w);
    }

    /// <summary>Em guerra e com dinheiro acima de ai_law_escalate_money, sobe um degrau de lei
    /// (o próximo sort do grupo). Em paz não mexe — voltar atrás não compensa o custo.</summary>
    private static void Laws(World w, Country c)
    {
        if (c.AtWarWith.Count == 0 || c.Money < w.Rule("ai_law_escalate_money", 120f)) return;
        foreach (var grp in w.Laws.Values.Select(l => l.Group).Distinct())
        {
            var cur = w.ActiveLaw(c, grp);
            var next = w.Laws.Values.Where(l => l.Group == grp && l.Sort == (cur?.Sort ?? 0) + 1)
                        .OrderBy(l => l.Id).FirstOrDefault();
            if (next is null) continue;
            var cmd = new ChangeLawCommand(c.Id, next.Id);
            if (cmd.Validate(w) is null) { cmd.Execute(w); return; }   // uma mudança por ronda
        }
    }

    /// <summary>Com dinheiro acima de ai_build_reserve, melhora a infraestrutura da região própria mais fraca
    /// (uma obra por ronda; BuildInfrastructureCommand valida custo e tecto).</summary>
    private static void Build(World w, Country c, List<Region>? controlled)
    {
        if (controlled is null || c.Money < w.Rule("ai_build_reserve", 150f)) return;
        // Em guerra: fortifica a região da frente com menos forte; em paz: infraestrutura na mais fraca.
        if (c.AtWarWith.Count > 0)
        {
            Region? front = null;
            foreach (var r in controlled)
                if (r.OwnerId == c.Id && !r.FortBuilding && r.Neighbours.Any(n => w.IsHostile(c.Id, w.Regions[n]))
                    && (front is null || r.Fort < front.Fort)) front = r;
            if (front is not null)
            {
                var fcmd = new BuildFortCommand(c.Id, front.Id);
                if (fcmd.Validate(w) is null) { fcmd.Execute(w); return; }
            }
        }
        Region? best = null;
        foreach (var r in controlled)
            if (r.OwnerId == c.Id && !r.Building && (best is null || r.Infrastructure < best.Infrastructure)) best = r;
        if (best is null) return;
        var cmd = new BuildInfrastructureCommand(c.Id, best.Id);
        if (cmd.Validate(w) is null) cmd.Execute(w);
    }

    /// <summary>Coligações: facções com guerras convidam países que lutam contra o mesmo inimigo
    /// (InviteToFactionCommand decide a aceitação — inimigo comum). O jogador nunca convida nem é
    /// convidado automaticamente; adere pelo painel do país.</summary>
    private static void FactionInvites(World w)
    {
        foreach (var f in w.Factions.Values.ToList())
        {
            int inviter = 0;
            var enemies = new HashSet<int>();
            foreach (var m in f.Members)
                if (w.Countries.TryGetValue(m, out var mc) && !mc.Capitulated)
                {
                    enemies.UnionWith(mc.AtWarWith);
                    if (inviter == 0 && !mc.IsPlayer) inviter = m;
                }
            if (inviter == 0 || enemies.Count == 0) continue;
            foreach (var c in w.Countries.Values.ToList())
            {
                if (c.IsPlayer || c.Capitulated || f.Members.Contains(c.Id)) continue;
                if (!c.AtWarWith.Overlaps(enemies)) continue;
                var cmd = new InviteToFactionCommand(inviter, f.Id, c.Id);
                if (cmd.Validate(w) is null) cmd.Execute(w);
            }
        }
    }

    /// <summary>Objectivo de guerra (HoI4: justificação): um país em paz com aggression &gt; 0 tenta, com probabilidade
    /// ai_war_chance × aggression por ronda a partir de ai_war_min_day, declarar guerra ao vizinho mais fraco cujo exército
    /// (+ o dos seus aliados de facção — dissuasão: atacar a Estónia é atacar a NATO inteira) seja ≤ o seu / ai_war_ratio.
    /// O jogador só é alvo a partir de ai_war_player_min_day. Uma guerra de cada vez. DeclareWarCommand.Validate já
    /// recusa aliados da própria facção, por isso nunca chegam a ser escolhidos como alvo.</summary>
    private static void WarGoal(World w, Country c, int myDivs, Dictionary<int, List<Division>> divsByCountry, List<Region>? owned)
    {
        float aggression = c.Stat("aggression", 0f);
        if (aggression <= 0f || owned is null || w.Clock.Day < w.Rule("ai_war_min_day", 30f)) return;
        if (w.Rng.NextDouble() >= w.Rule("ai_war_chance", 0.02f) * aggression) return;
        float ratio = w.Rule("ai_war_ratio", 2f);
        Country? target = null; int targetDivs = int.MaxValue;
        foreach (var r in owned)
            foreach (var n in r.SeaNeighbours.Count == 0 ? r.Neighbours : r.Neighbours.Concat(r.SeaNeighbours.Keys))
            {
                int other = w.Regions[n].ControllerId;
                if (other == c.Id || !w.Countries.TryGetValue(other, out var o) || o.Capitulated) continue;
                if (o.IsPlayer && w.Clock.Day < w.Rule("ai_war_player_min_day", 90f)) continue;
                int theirs = divsByCountry.GetValueOrDefault(other)?.Count ?? 0;
                foreach (var ally in w.Allies(other)) theirs += divsByCountry.GetValueOrDefault(ally)?.Count ?? 0;
                if (theirs * ratio > myDivs) continue;
                if (theirs < targetDivs || (theirs == targetDivs && target is not null && other < target.Id)) { target = o; targetDivs = theirs; }
            }
        if (target is null) return;
        if (c.JustifyTarget is not null) return;   // já a justificar um objectivo
        var cmd = new JustifyWarCommand(c.Id, target.Id);
        if (cmd.Validate(w) is null) cmd.Execute(w);
    }

    /// <summary>Sem investigação em curso → a tecnologia disponível mais barata (HoI4: a IA nunca deixa um slot vazio).</summary>
    private static void Research(World w, Country c)
    {
        if (c.ResearchTech is not null || w.Techs.Count == 0) return;
        Tech? best = null;
        foreach (var t in w.Techs.Values)
            if (w.CanResearch(c, t.Id) && (best is null || t.Cost < best.Cost || (t.Cost == best.Cost && string.CompareOrdinal(t.Id, best.Id) < 0))) best = t;
        if (best is null) return;
        var cmd = new ResearchTechCommand(c.Id, best.Id);
        if (cmd.Validate(w) is null) cmd.Execute(w);
    }

    /// <summary>Mantém ai_max_queue encomendas: normalmente o template mais barato; cada ai_heavy_every-ésima
    /// (contando divisões existentes + fila) o de maior breakthrough, se o orçamento chegar. O dinheiro não é
    /// gasto aqui (ProductionSystem fá-lo aos poucos), mas cada encomenda desta ronda abate no orçamento
    /// para a IA não encomendar acima do que tem.</summary>
    private static void Produce(World w, Country c, int existing)
    {
        var templates = w.Units.GetTemplates(c.Id);
        if (templates.Count == 0) return;
        int maxQueue = (int)w.Rule("ai_max_queue", 3), heavyEvery = Math.Max(1, (int)w.Rule("ai_heavy_every", 3));
        var cheapest = templates.MinBy(t => w.TemplateCost(t.Id))!;
        var strongest = templates.MaxBy(t => w.Stats.Get(t.Id)["breakthrough"])!;
        float budget = c.Money;
        while (c.Queue.Count < maxQueue)
        {
            int order = existing + c.Queue.Count + 1;
            var pick = order % heavyEvery == 0 && budget >= w.TemplateCost(strongest.Id) ? strongest : cheapest;
            float cost = w.TemplateCost(pick.Id);
            if (budget < cost) return;
            var cmd = new BuildDivisionCommand(c.Id, pick.Id);
            if (cmd.Validate(w) is not null) return;
            cmd.Execute(w); budget -= cost;
        }
    }

    /// <summary>Frente = regiões nossas com vizinho inimigo. Em cada região da frente com divisões disponíveis:
    /// alvo = região inimiga adjacente com menos defensores; vazia → 1 divisão (se sobra guarnição ou não há
    /// ameaça ao lado); defendida → grupo inteiro se ≥ defensores × ai_attack_ratio; senão fica a defender.
    /// Retaguarda → região da frente mais próxima por território próprio (BFS multi-fonte a partir da frente).</summary>
    private static void Fight(World w, Country c, List<Division> divs, List<Region>? owned,
        Dictionary<int, Dictionary<int, int>> fighters, HashSet<int> inBattle)
    {
        if (owned is null) return;
        // Frente = fronteira terrestre hostil + costas com inimigo ao alcance do mar (guarnição costeira:
        // sem isto os desembarques inimigos só eram contra-atacados depois de aterrar).
        var front = owned.Where(r => r.Neighbours.Any(n => w.IsHostile(c.Id, w.Regions[n]))
                                  || r.SeaNeighbours.Keys.Any(n => w.IsHostile(c.Id, w.Regions[n]))).ToList();
        if (front.Count == 0) { Expedition(w, c, divs, inBattle); return; }

        float minOrg = w.Rule("ai_min_org", 50), ratio = w.Rule("ai_attack_ratio", 1.5f);
        var groups = new Dictionary<int, List<Division>>();   // região → divisões disponíveis
        foreach (var d in divs)
        {
            if (d.Path.Count > 0 || d.Org < minOrg || !d.CanFight || inBattle.Contains(d.Id)) continue;
            if (!groups.TryGetValue(d.RegionId, out var g)) groups[d.RegionId] = g = new();
            g.Add(d);
        }
        if (groups.Count == 0) return;

        var frontIds = new HashSet<int>(front.Select(r => r.Id));
        foreach (var f in front)
        {
            if (!groups.TryGetValue(f.Id, out var g)) continue;
            Region? target = null; int best = int.MaxValue, total = 0;
            foreach (var n in f.SeaNeighbours.Count == 0 ? f.Neighbours : f.Neighbours.Concat(f.SeaNeighbours.Keys))
            {
                var r = w.Regions[n];
                if (!w.IsHostile(c.Id, r)) continue;
                int def = Defenders(c, r.Id, fighters); total += def;
                if (def < best) { best = def; target = r; }
            }
            if (target is null) continue;
            if (best == 0) { if (g.Count >= 2 || total == 0) Send(w, c, g.Take(1), target.Id); }
            else if (g.Count >= best * ratio) Send(w, c, g, target.Id);
        }

        var nearest = new Dictionary<int, int>();   // região nossa → região da frente mais próxima
        var queue = new Queue<int>();
        foreach (var f in front) { nearest[f.Id] = f.Id; queue.Enqueue(f.Id); }
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (var n in w.Regions[cur].Neighbours)
            {
                if (nearest.ContainsKey(n) || w.Regions[n].ControllerId != c.Id) continue;
                nearest[n] = nearest[cur]; queue.Enqueue(n);
            }
        }
        foreach (var (regionId, g) in groups)
            if (!frontIds.Contains(regionId) && nearest.TryGetValue(regionId, out var dest)) Send(w, c, g, dest);
    }

    /// <summary>Corpo expedicionário: sem frente própria mas em guerra, as divisões paradas vão
    /// defender a frente de um aliado de facção que partilhe inimigo (acesso militar). O destino é a
    /// região da frente aliada com menos divisões amigas — o buraco mais aberto.</summary>
    private static void Expedition(World w, Country c, List<Division> divs, HashSet<int> inBattle)
    {
        float minOrg = w.Rule("ai_min_org", 50);
        var idle = divs.Where(d => d.Path.Count == 0 && d.Org >= minOrg && d.CanFight && !inBattle.Contains(d.Id)).ToList();
        if (idle.Count == 0) return;
        var allies = w.Allies(c.Id).Where(a => w.Countries.TryGetValue(a, out var ac) && !ac.Capitulated
                                            && ac.AtWarWith.Any(c.AtWarWith.Contains)).ToHashSet();
        if (allies.Count == 0) return;

        Region? target = null; int fewest = int.MaxValue;
        foreach (var r in w.Regions.Values)
        {
            if (!allies.Contains(r.ControllerId)) continue;
            if (!r.Neighbours.Any(n => w.IsHostile(c.Id, w.Regions[n]))) continue;
            int friends = r.DivisionIds.Count;
            if (friends < fewest) { fewest = friends; target = r; }
        }
        if (target is not null) Send(w, c, idle, target.Id);
    }

    /// <summary>Retira de batalhas muito desequilibradas: org própria &lt; org do outro lado
    /// × ai_retreat_ratio → RetreatFromBattleCommand (salva as divisões à custa de organização).</summary>
    private static void Retreats(World w, Country c)
    {
        float ratio = w.Rule("ai_retreat_ratio", 0.25f);
        foreach (var b in w.ActiveBattles.ToList())
        {
            float mineAtt = 0f, mineDef = 0f, otherAtt = 0f, otherDef = 0f;
            foreach (var id in b.Attackers)
                if (w.Divisions.TryGetValue(id, out var d)) { if (d.CountryId == c.Id) mineAtt += d.Org; else otherAtt += d.Org; }
            foreach (var id in b.Defenders)
                if (w.Divisions.TryGetValue(id, out var d)) { if (d.CountryId == c.Id) mineDef += d.Org; else otherDef += d.Org; }
            float mine = mineAtt + mineDef;
            if (mine <= 0f) continue;
            // se ataco, o inimigo é o lado defensor (e vice-versa); org inimiga inclui a de terceiros na batalha
            float enemy = mineAtt > 0f ? SumOrg(w, b.Defenders) - mineDef : SumOrg(w, b.Attackers) - mineAtt;
            if (mine >= enemy * ratio) continue;
            var cmd = new Commands.RetreatFromBattleCommand(c.Id, b.RegionId);
            if (cmd.Validate(w) is null) cmd.Execute(w);
        }
    }

    private static float SumOrg(World w, List<int> ids)
    {
        float s = 0f;
        foreach (var id in ids) if (w.Divisions.TryGetValue(id, out var d)) s += d.Org;
        return s;
    }

    /// <summary>Divisões CanFight de países com quem `c` está em guerra, numa região.</summary>
    private static int Defenders(Country c, int regionId, Dictionary<int, Dictionary<int, int>> fighters)
    {
        if (!fighters.TryGetValue(regionId, out var byCountry)) return 0;
        int n = 0;
        foreach (var e in c.AtWarWith) n += byCountry.GetValueOrDefault(e);
        return n;
    }

    private static void Send(World w, Country c, IEnumerable<Division> divs, int target)
    {
        foreach (var d in divs)
        {
            var cmd = new MoveDivisionCommand(c.Id, d.Id, target);
            if (cmd.Validate(w) is null) cmd.Execute(w);
        }
    }
}
