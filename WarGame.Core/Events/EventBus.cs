namespace WarGame.Core.Events;

public interface IGameEvent { }

public sealed record DayPassed(int Day) : IGameEvent;
public sealed record WarDeclared(int Aggressor, int Target) : IGameEvent;
/// <summary>Um membro de uma facção do alvo é chamado à guerra contra o agressor (DeclareWarCommand: só a facção do defensor chama).</summary>
public sealed record FactionJoinedWar(string FactionId, int MemberCountryId, int AgainstCountryId) : IGameEvent;
public sealed record RegionCaptured(int RegionId, int OldController, int NewController) : IGameEvent;
public sealed record BattleStarted(int RegionId) : IGameEvent;
/// <summary>Batalha resolvida. Traz os dois beligerantes para quem conta estatísticas não ter de
/// adivinhar quem lá estava (a batalha já saiu de ActiveBattles quando isto é publicado).</summary>
public sealed record BattleEnded(int RegionId, bool AttackerWon, int AttackerCountryId, int DefenderCountryId) : IGameEvent;
public sealed record DivisionDestroyed(int DivisionId) : IGameEvent;
public sealed record TechResearched(int CountryId, string TechId) : IGameEvent;
/// <summary>Um país capitulou (PeaceSystem); Winner ficou com as regiões que o capitulado ainda controlava.</summary>
public sealed record CountryCapitulated(int CountryId, int WinnerId) : IGameEvent;
public sealed record WarEnded(int A, int B) : IGameEvent;
/// <summary>Um beligerante fixou o que quer desta guerra (WarGoalSystem): as regiões exigidas ao inimigo.</summary>
public sealed record WarGoalDeclared(int CountryId, int TargetCountryId, IReadOnlyList<int> RegionIds) : IGameEvent;
/// <summary>Todas as regiões do objectivo estão nas mãos de quem as exigiu — a guerra já deu o que tinha a dar.</summary>
public sealed record WarGoalAchieved(int CountryId, int TargetCountryId) : IGameEvent;
/// <summary>Saldo de uma guerra que acabou de terminar (WarStatsSystem), já guardado em World.WarHistory.</summary>
public sealed record WarSummary(WarGame.Core.Model.WarRecord Record) : IGameEvent;
/// <summary>Divisão condecorada (MedalSystem): id da divisão, do seu país e da medalha.</summary>
public sealed record MedalAwarded(int DivisionId, int CountryId, string MedalId) : IGameEvent;
/// <summary>Uma divisão passou a ter nome próprio: ganhou (ou subiu de) honra de batalha.</summary>
public sealed record DivisionHonoured(int DivisionId, int CountryId, string HonourId, string Title) : IGameEvent;
/// <summary>Paz branca por estagnação (TruceSystem); sai sempre antes do WarEnded da mesma guerra.</summary>
public sealed record WhitePeaceSigned(int A, int B) : IGameEvent;
/// <summary>Paz negociada: o vencedor ficou com Regions regiões do derrotado.</summary>
public sealed record PeaceSigned(int Winner, int Loser, int Regions) : IGameEvent;
/// <summary>Desembarque desistido: a divisão chegou à costa inimiga sem organização para assaltar.</summary>
public sealed record LandingAborted(int DivisionId, int RegionId) : IGameEvent;
/// <summary>Um país controla ≥ victory_pop_share da população mundial (VictorySystem, uma vez por jogo).</summary>
public sealed record WorldDominated(int CountryId) : IGameEvent;
/// <summary>Template desenhado em jogo (CreateTemplateCommand).</summary>
public sealed record TemplateCreated(int CountryId, int TemplateId) : IGameEvent;
/// <summary>Evento noticioso do jogador com escolhas por fazer (a UI abre o diálogo).</summary>
public sealed record NewsChoiceRequired(string EventId) : IGameEvent;
/// <summary>Escolha feita num evento com opções (ChooseNewsOptionCommand ou IA).</summary>
public sealed record NewsChoiceMade(int CountryId, string EventId, string OptionId) : IGameEvent;
/// <summary>Facção fundada em jogo (CreateFactionCommand).</summary>
public sealed record FactionCreated(int CountryId, string FactionId) : IGameEvent;
/// <summary>País entrou numa facção (convite aceite ou adesão).</summary>
public sealed record FactionJoined(string FactionId, int CountryId) : IGameEvent;
/// <summary>Convite recusado (sem inimigo comum — World.FactionWouldAccept).</summary>
public sealed record FactionInviteRejected(string FactionId, int CountryId) : IGameEvent;
/// <summary>País saiu de uma facção (LeaveFactionCommand).</summary>
public sealed record FactionLeft(string FactionId, int CountryId) : IGameEvent;
/// <summary>Obra de infraestrutura concluída (ConstructionSystem).</summary>
public sealed record InfrastructureBuilt(int RegionId) : IGameEvent;
/// <summary>Lei nacional mudada (ChangeLawCommand).</summary>
public sealed record LawChanged(int CountryId, string LawId) : IGameEvent;
/// <summary>Nível de fortificação concluído (ConstructionSystem).</summary>
public sealed record FortBuilt(int RegionId, int Level) : IGameEvent;
/// <summary>Pontos de produção enviados a um aliado (TransferMoneyCommand).</summary>
public sealed record MoneyTransferred(int FromCountryId, int ToCountryId, float Amount) : IGameEvent;
/// <summary>Proposta de paz branca recusada (OfferPeaceCommand: a IA ainda acha que ganha).</summary>
public sealed record PeaceOfferRejected(int FromCountryId, int ToCountryId) : IGameEvent;
/// <summary>Região ocupada revoltou-se e voltou ao dono (ResistanceSystem).</summary>
public sealed record RegionRevolted(int RegionId, int OldController) : IGameEvent;
/// <summary>Acordo de comércio criado (CreateTradeDealCommand).</summary>
public sealed record TradeDealCreated(int BuyerId, int SellerId, string ResourceId, float Units) : IGameEvent;
/// <summary>Acordo de comércio terminado (cancelado ou caiu: guerra, depósitos, dinheiro).</summary>
public sealed record TradeDealEnded(int BuyerId, int SellerId, string ResourceId) : IGameEvent;
public sealed record SpyOpStarted(int CountryId, int TargetCountryId, string OpId) : IGameEvent;
public sealed record SpyOpCompleted(int CountryId, int TargetCountryId, string OpId) : IGameEvent;
public sealed record DivisionDisbanded(int DivisionId, int CountryId) : IGameEvent;
public sealed record PactSigned(int A, int B, int UntilDay) : IGameEvent;
public sealed record PactRejected(int FromCountryId, int ToCountryId) : IGameEvent;
public sealed record AirWingBought(int CountryId, int Total) : IGameEvent;
public sealed record BattleRetreat(int RegionId, int CountryId, int Divisions) : IGameEvent;
public sealed record FocusCompleted(int CountryId, string FocusId) : IGameEvent;
public sealed record WarJustifyStarted(int CountryId, int TargetCountryId) : IGameEvent;
public sealed record NewsFired(string EventId) : IGameEvent;
/// <summary>Infraestrutura danificada voltou ao valor de origem (InfrastructureRepairSystem).</summary>
public sealed record InfrastructureRepaired(int RegionId) : IGameEvent;
/// <summary>Região ocupada integrada no país do controlador (IntegrationSystem): OwnerId mudou.</summary>
public sealed record RegionIntegrated(int RegionId, int OldOwner, int NewOwner) : IGameEvent;
/// <summary>Comandante contratado (HireGeneralCommand).</summary>
public sealed record GeneralHired(int CountryId, string GeneralId) : IGameEvent;
/// <summary>Um país mudou de lugar na tabela mundial de potências (PowerRankingSystem).
/// From = 0 quando ainda não tinha classificação.</summary>
public sealed record PowerRankChanged(int CountryId, int From, int To, string Tier) : IGameEvent;
/// <summary>Comandante promovido pelas batalhas do grupo que comanda (GeneralXpSystem).</summary>
public sealed record GeneralPromoted(int CountryId, string GeneralId, int Level, string RankName) : IGameEvent;
/// <summary>Comandante dispensado (DismissGeneralCommand).</summary>
public sealed record GeneralDismissed(int CountryId, string GeneralId) : IGameEvent;
/// <summary>Decisão nacional activada (ActivateDecisionCommand).</summary>
public sealed record DecisionActivated(int CountryId, string DecisionId) : IGameEvent;
/// <summary>Decisão nacional expirou (DecisionSystem).</summary>
public sealed record DecisionExpired(int CountryId, string DecisionId) : IGameEvent;
/// <summary>Edifício concluído numa região (ConstructionSystem): nível novo.</summary>
public sealed record BuildingBuilt(int RegionId, string BuildingId, int Level) : IGameEvent;
public sealed record NukeBuilt(int CountryId, int Total) : IGameEvent;
/// <summary>Ataque nuclear a uma região: divisões e infra-estrutura arrasadas, estabilidade dos dois lados sofre.</summary>
public sealed record NukeStruck(int AttackerId, int RegionId, int TargetCountryId, int DivisionsHit) : IGameEvent;

/// <summary>Pub/sub tipado. UI e sistemas subscrevem; ninguém chama ninguém directamente.</summary>
public sealed class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subs = new();

    public IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent
    {
        if (!_subs.TryGetValue(typeof(T), out var list)) _subs[typeof(T)] = list = new();
        list.Add(handler);
        return new Unsub(() => list.Remove(handler));
    }

    public void Publish<T>(T evt) where T : IGameEvent
    {
        if (!_subs.TryGetValue(typeof(T), out var list)) return;
        foreach (var d in list.ToArray()) ((Action<T>)d)(evt);
    }

    private sealed class Unsub : IDisposable
    {
        private readonly Action _a; public Unsub(Action a) => _a = a; public void Dispose() => _a();
    }
}
