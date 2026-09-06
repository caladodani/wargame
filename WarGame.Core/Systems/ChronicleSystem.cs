using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A memória da campanha. O Jornal só guardava os avisos da sessão em curso: fechava-se o jogo e a
/// guerra de há dois anos ficava sem história nenhuma. A crónica ouve os acontecimentos que valem a pena
/// lembrar — declarações de guerra, capitais tomadas, capitulações, pazes, bombas, tropas que ganharam nome —
/// escreve-os por ordem e vai no save.
///
/// Não decide nada: só ouve. O que entra e o que fica de fora é dado (tabela chronicle_kind e a regra
/// chronicle_min_weight); o tecto de entradas é chronicle_max, e o que transborda são as mais antigas.</summary>
public sealed class ChronicleSystem : ISystem
{
    public string Name => "Chronicle";
    private World? _bound;

    public void Tick(World w)
    {
        if (!ReferenceEquals(_bound, w)) Bind(w);
    }

    /// <summary>Liga-se ao barramento. Idempotente por mundo: um save carregado traz um World novo e volta a
    /// subscrever; o mesmo mundo nunca subscreve duas vezes.</summary>
    public void Bind(World w)
    {
        _bound = w;

        w.Events.Subscribe<WarDeclared>(e =>
            Write(w, "guerra", $"{Who(w, e.Aggressor)} declara guerra a {Who(w, e.Target)}.", e.Aggressor));
        w.Events.Subscribe<PeaceSigned>(e =>
            Write(w, "paz", $"{Who(w, e.Winner)} e {Who(w, e.Loser)} assinam a paz — {e.Regions} regiões mudam de dono.", e.Winner));
        w.Events.Subscribe<WhitePeaceSigned>(e =>
            Write(w, "paz", $"{Who(w, e.A)} e {Who(w, e.B)} assinam paz branca: tudo fica como estava.", e.A));
        w.Events.Subscribe<CountryCapitulated>(e =>
            Write(w, "capitulacao", $"{Who(w, e.CountryId)} capitula perante {Who(w, e.WinnerId)}.", e.CountryId));
        w.Events.Subscribe<WorldDominated>(e =>
            Write(w, "dominio", $"{Who(w, e.CountryId)} manda no mundo inteiro.", e.CountryId));
        w.Events.Subscribe<NukeStruck>(e =>
            Write(w, "bomba", $"Bomba atómica de {Who(w, e.AttackerId)} sobre {Place(w, e.RegionId)}: {e.DivisionsHit} divisões apanhadas.", e.AttackerId, e.RegionId));
        w.Events.Subscribe<RegionRevolted>(e =>
            Write(w, "revolta", $"{Place(w, e.RegionId)} levanta-se contra {Who(w, e.OldController)}.", e.OldController, e.RegionId));
        w.Events.Subscribe<DivisionHonoured>(e =>
            Write(w, "honra", $"Uma divisão de {Who(w, e.CountryId)} passa a chamar-se «{e.Title}».", e.CountryId));
        w.Events.Subscribe<PrisonersReturned>(e =>
            Write(w, "prisioneiros", $"{Who(w, e.HolderId)} devolve {e.Men:N0} prisioneiros a {Who(w, e.HomeCountryId)}.", e.HomeCountryId));
        w.Events.Subscribe<GeneralKilled>(e =>
            Write(w, "baixa", $"{GeneralName(w, e.GeneralId)} morre em combate em {Place(w, e.RegionId)} ao serviço de {Who(w, e.CountryId)}.", e.CountryId, e.RegionId));
        w.Events.Subscribe<GeneralWounded>(e =>
            Write(w, "baixa", $"{GeneralName(w, e.GeneralId)} ({Who(w, e.CountryId)}) sai ferido do campo: {e.Days} dias fora de serviço.", e.CountryId));
        w.Events.Subscribe<GeneralPromoted>(e =>
            Write(w, "promocao", $"{Who(w, e.CountryId)}: {GeneralName(w, e.GeneralId)} promovido a {e.RankName}.", e.CountryId));
        w.Events.Subscribe<FocusCompleted>(e =>
            Write(w, "foco", $"{Who(w, e.CountryId)} conclui {FocusName(w, e.FocusId)}.", e.CountryId));
        w.Events.Subscribe<FactionCreated>(e =>
            Write(w, "alianca", $"{Who(w, e.CountryId)} funda a aliança {FactionName(w, e.FactionId)}.", e.CountryId));
        w.Events.Subscribe<FactionJoined>(e =>
            Write(w, "alianca", $"{Who(w, e.CountryId)} entra na aliança {FactionName(w, e.FactionId)}.", e.CountryId));
        w.Events.Subscribe<SeasonChanged>(e =>
            Write(w, "estacao", $"Entrou o {e.Name}.", 0));
        // Capturas há às centenas numa guerra grande: só a queda de uma capital entra na crónica.
        w.Events.Subscribe<RegionCaptured>(e =>
        {
            if (!w.Regions.TryGetValue(e.RegionId, out var r)) return;
            var fallen = w.Countries.Values.FirstOrDefault(c => c.CapitalRegionId == r.Id);
            if (fallen is null) return;
            Write(w, "capital", $"{Who(w, e.NewController)} toma {r.Name}, capital de {fallen.Name}.", e.NewController, r.Id);
        });
    }

    /// <summary>Escreve uma entrada, se o género existir e pesar o bastante. Devolve o que ficou escrito (ou
    /// null), para quem chama de fora poder confirmar sem ir ler a lista.</summary>
    public static ChronicleEntry? Write(World w, string kind, string text, int countryId, int regionId = 0)
    {
        if (!w.ChronicleKinds.TryGetValue(kind, out var def)) return null;
        if (def.Weight < (int)w.Rule("chronicle_min_weight", 1f)) return null;

        var entry = new ChronicleEntry(w.Clock.Day, kind, text, countryId, regionId);
        w.Chronicle.Add(entry);
        int max = Math.Max(1, (int)w.Rule("chronicle_max", 400f));
        if (w.Chronicle.Count > max) w.Chronicle.RemoveRange(0, w.Chronicle.Count - max);
        return entry;
    }

    private static string Who(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) ? c.Name : "um país";
    private static string Place(World w, int regionId) =>
        w.Regions.TryGetValue(regionId, out var r) ? r.Name : "uma região";
    private static string GeneralName(World w, string id) =>
        w.GeneralDefs.TryGetValue(id, out var g) ? g.Name : id;
    private static string FocusName(World w, string id) =>
        w.Focuses.TryGetValue(id, out var f) ? f.Name : id;
    private static string FactionName(World w, string id) =>
        w.Factions.TryGetValue(id, out var f) ? f.Name : id;
}
