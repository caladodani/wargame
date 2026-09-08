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
/// <summary>Uma divisão cercada baixou as armas (PocketSystem). Traz quem fechou o cerco porque, ao
/// contrário de uma divisão desfeita em combate, esta rende-se em terreno que ainda era do seu país: o
/// captor não se descobre olhando para quem manda na região.</summary>
public sealed record DivisionSurrendered(int DivisionId, int CaptorId, int CountryId, int RegionId) : IGameEvent;
public sealed record TechResearched(int CountryId, string TechId) : IGameEvent;
/// <summary>Um país capitulou (PeaceSystem); Winner ficou com as regiões que o capitulado ainda controlava.</summary>
public sealed record CountryCapitulated(int CountryId, int WinnerId) : IGameEvent;
/// <summary>O governo de um país capitulado embarcou para o exílio (ExileSystem): quem o acolhe.</summary>
public sealed record GovernmentExiled(int CountryId, int HostId) : IGameEvent;
/// <summary>O governo no exílio mudou de casa: o anfitrião caiu e outro aliado recolheu-o.</summary>
public sealed record ExileMoved(int CountryId, int OldHostId, int HostId) : IGameEvent;
/// <summary>Acabou-se o exílio sem regresso (ExileSystem): ficou sem quem o acolhesse, ou sem legitimidade
/// nenhuma. O país continua capitulado — o que morreu foi o governo que ainda se dizia dele.</summary>
public sealed record ExileEnded(int CountryId) : IGameEvent;
/// <summary>O governo voltou do exílio (ExileSystem): quem libertou a capital, quantas regiões lhe foram
/// devolvidas e com quantas divisões de exílio regressou.</summary>
public sealed record GovernmentReturned(int CountryId, int LiberatorId, int Regions, int Divisions) : IGameEvent;
/// <summary>Um vencedor levou a sua parte na conferência de paz (PeaceSpoils): quantas regiões do
/// derrotado e quantos pontos de espólio pagou por elas.</summary>
public sealed record SpoilsTaken(int WinnerId, int LoserId, int Regions, float Points) : IGameEvent;
/// <summary>A frente contra um inimigo passou mais um degrau de avanço (TheatreSystem): a fatia da terra dele
/// que já controlamos, e em quantos troços de frente a guerra vai.</summary>
public sealed record FrontAdvanced(int CountryId, int FoeId, float Progress, int Theatres) : IGameEvent;
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
/// <summary>Mudou a estação do ano (WeatherSystem): a marcha, a recomposição e o desgaste mudam com ela.</summary>
public sealed record SeasonChanged(string SeasonId, string Name, int Day) : IGameEvent;

/// <summary>Prisioneiros de guerra (PrisonerSystem): homens que mudaram de mãos sem morrer.</summary>
public sealed record PrisonersTaken(int CaptorId, int FromCountryId, int Men, int RegionId) : IGameEvent;
public sealed record PrisonersReturned(int HolderId, int HomeCountryId, int Men) : IGameEvent;
/// <summary>Troca negociada de prisioneiros (ExchangePrisonersCommand): Men homens de cada lado saíram
/// dos campos e Home de cada lado chegaram a casa.</summary>
public sealed record PrisonersExchanged(int CountryId, int OtherId, int Men, int Home) : IGameEvent;

/// <summary>Propostas em cima da mesa (OfferSystem): feita ao jogador, respondida, ou caída por prazo.</summary>
public sealed record OfferMade(int FromId, int ToId, string Kind, int Men, int RegionId = 0) : IGameEvent;
public sealed record OfferAnswered(int FromId, int ToId, string Kind, bool Accepted) : IGameEvent;
public sealed record OfferExpired(int FromId, int ToId, string Kind) : IGameEvent;

/// <summary>Combate travado no céu de uma região (AirMissionSystem) ou no mar de uma costa
/// (NavalMissionSystem), do ponto de vista de UM dos lados: Lost é o que este país deixou lá e Worse diz
/// que foi ele quem levou a pior parte (perdeu a maior fatia do que tinha lá). Sai um por cada lado.
///
/// Existem para o comando do ar e do mar poder cair como cai o de terra: sem eles, o CommandCasualtySystem
/// só sabia de batalhas em terra e um almirante nunca corria risco nenhum.</summary>
public sealed record AirCombatEnded(int RegionId, int CountryId, int EnemyCountryId, float Lost, bool Worse) : IGameEvent;
public sealed record SeaCombatEnded(int RegionId, int CountryId, int EnemyCountryId, float Lost, bool Worse) : IGameEvent;

/// <summary>Baixas no comando (CommandCasualtySystem): o comandante caiu na batalha daquela região.</summary>
public sealed record GeneralWounded(int CountryId, string GeneralId, string KindId, int Days) : IGameEvent;
public sealed record GeneralKilled(int CountryId, string GeneralId, int RegionId) : IGameEvent;
public sealed record GeneralRecovered(int CountryId, string GeneralId) : IGameEvent;
/// <summary>O exército mudou de comandante sem ninguém o mandar: o anterior caiu. NewGeneralId a null =
/// ficou sem comando.</summary>
public sealed record CommandHandedOver(int CountryId, int GroupId, string? NewGeneralId) : IGameEvent;
/// <summary>Paz branca por estagnação (TruceSystem); sai sempre antes do WarEnded da mesma guerra.</summary>
/// <summary>Uma região mudou de dono à mesa, sem ninguém a ter tomado: cedência negociada.</summary>
public sealed record RegionCeded(int FromId, int ToId, int RegionId) : IGameEvent;
public sealed record WhitePeaceSigned(int A, int B) : IGameEvent;
/// <summary>Paz negociada: o vencedor ficou com Regions regiões do derrotado.</summary>
public sealed record PeaceSigned(int Winner, int Loser, int Regions) : IGameEvent;
/// <summary>Desembarque desistido: a divisão chegou à costa inimiga sem organização para assaltar.</summary>
public sealed record LandingAborted(int DivisionId, int RegionId) : IGameEvent;
/// <summary>Os transportes levantaram com os pára-quedistas a bordo (ParadropSystem).</summary>
public sealed record ParadropLaunched(int DivisionId, int CountryId, int FromRegionId, int TargetRegionId, float Days) : IGameEvent;
/// <summary>Os pára-quedistas caíram na região; Captured = o terreno era do inimigo e passou a ser nosso.</summary>
public sealed record ParadropLanded(int DivisionId, int CountryId, int RegionId, bool Captured) : IGameEvent;
/// <summary>O salto foi por água abaixo em voo (o chão deixou de estar livre) e a tropa voltou ao ponto de partida.</summary>
public sealed record ParadropAborted(int DivisionId, int CountryId, int RegionId, string Why) : IGameEvent;
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
/// <summary>Via férrea assente numa região (ConstructionSystem): a rede de abastecimento passa a contar
/// com ela e o mapa desenha-lhe mais uma linha de comboio.</summary>
public sealed record RailBuilt(int RegionId, int Level) : IGameEvent;
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
/// <summary>Empréstimo de material assinado ou revisto (LendLeaseCommand): Share é a fatia nova.</summary>
public sealed record LendLeaseSigned(int FromCountryId, int ToCountryId, float Share) : IGameEvent;
/// <summary>Empréstimo de material fechado: por ordem, por guerra entre os dois ou por capitulação.
/// SentTotal é o que chegou a entrar no cofre do destinatário durante toda a vida do acordo.</summary>
public sealed record LendLeaseEnded(int FromCountryId, int ToCountryId, float SentTotal) : IGameEvent;
public sealed record SpyOpStarted(int CountryId, int TargetCountryId, string OpId) : IGameEvent;
public sealed record SpyOpCompleted(int CountryId, int TargetCountryId, string OpId) : IGameEvent;
/// <summary>Sabotagem consumada numa região do inimigo: o quê, onde e o estrago em texto curto.</summary>
public sealed record RegionSabotaged(int CountryId, int TargetCountryId, string OpId, int RegionId, string Damage) : IGameEvent;
/// <summary>Equipa de sabotagem apanhada na retaguarda antes de fazer o estrago.</summary>
public sealed record SabotageFoiled(int CountryId, int TargetCountryId, string OpId, int RegionId) : IGameEvent;
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
/// <summary>Conselheiro civil nomeado para uma pasta do gabinete (AppointAdvisorCommand).</summary>
public sealed record AdvisorAppointed(int CountryId, string AdvisorId, string Slot) : IGameEvent;
/// <summary>Conselheiro que deixou o gabinete: demitido pelo país ou saído por falta de pagamento
/// (Quit=true, CabinetSystem).</summary>
public sealed record AdvisorLeft(int CountryId, string AdvisorId, string Slot, bool Quit) : IGameEvent;
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
/// <summary>Batalha perdida (DefeatAlarmSystem), com a conta das derrotas seguidas em que ela entra.
/// GroundLost = a região mudou de mãos; Alarm = a série chegou à regra defeat_streak_alarm e o país
/// pagou-a em desgaste de guerra. A UI toca o klaxon e acende a faixa; a crónica só escreve as de alarme.</summary>
public sealed record BattleLost(int CountryId, int RegionId, int Streak, bool GroundLost, bool Alarm) : IGameEvent;
/// <summary>Um país passou a estado-fantoche de outro: mantém bandeira e terra, paga tributo e homens.</summary>
public sealed record SubjectMade(int SubjectId, int OverlordId) : IGameEvent;
/// <summary>A autonomia chegou ao topo e o vassalo levantou-se: já não paga nada a ninguém.</summary>
public sealed record SubjectFreed(int SubjectId, int OverlordId) : IGameEvent;

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
