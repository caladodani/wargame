using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A guerra submarina: o que se esconde não leva tiro, e vai-se lá buscar com sonar.
///
/// O submarino era um navio como os outros — dava mais bloqueio e ia ao fundo no combate de esquadra ao
/// lado dos cruzadores. Faltava o que faz de um submarino um submarino: andar escondido. E do outro lado
/// faltava a resposta — o contratorpedeiro dizia na ficha que caçava submarinos e não tinha uma linha de
/// código que o fizesse.
///
/// O que estes testes guardam: a conta do esconderijo (busca contra bando), que o escondido não leva nem dá
/// tiro no combate de esquadra nem do ar, que só a missão de caça o afunda e só afunda o que vê, e que a IA
/// manda a caça quando lhe metem submarinos no mar de casa.</summary>
public class SubTests
{
    private const int Mar = 7;      // um mar ao largo, longe de toda a terra: é lá que o bando se esconde

    /// <summary>Linha do costume mais um mar ao largo. Os dois países de mão humana (a IA tem o seu próprio
    /// teste) e cofre cheio, para nenhuma esquadra voltar ao porto por falta de dinheiro.</summary>
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Regions[Mar] = new Region
        {
            Id = Mar, Name = "M" + Mar, OwnerId = 2, InitialOwnerId = 2, ControllerId = 2,
            Terrain = "plain", Population = 1000, Lon = 60f, CenterX = 700, CenterY = 500, Coastal = true,
        };
        // rotas de mar: da nossa costa (3) para o largo e para a costa deles, senão nenhuma esquadra tem
        // por onde ir e a caça nunca sai do porto
        w.Regions[3].SeaNeighbours[Mar] = 500f; w.Regions[Mar].SeaNeighbours[3] = 500f;
        w.Regions[3].SeaNeighbours[4] = 400f; w.Regions[4].SeaNeighbours[3] = 400f;
        w.StartWar(1, 2);
        foreach (var c in w.Countries.Values) { c.Money = 5000f; c.IsPlayer = true; c.Ships.Clear(); }
        w.Register(new NavalMissionSystem());
        return w;
    }

    /// <summary>Esquadra escrita à mão: o que interessa é a composição, não quem a escolheu.</summary>
    private static NavalMission Esquadra(World w, int countryId, int regionId, string missionId,
                                         params (string Class, float N)[] squadron)
    {
        var m = new NavalMission { CountryId = countryId, RegionId = regionId, MissionId = missionId, Name = "E" };
        foreach (var (cls, n) in squadron) m.Squadron[cls] = n;
        w.NavalMissions.Add(m);
        foreach (var (cls, n) in squadron)
            if (w.Countries.TryGetValue(countryId, out var c)) c.Ships[cls] = c.Ships.GetValueOrDefault(cls) + n;
        return m;
    }

    /// <summary>Esconder-se e caçar vêm os dois da tabela: só o submarino tem stealth e o contratorpedeiro é
    /// o melhor sonar que há. Nada disto está escrito em código.</summary>
    [Fact]
    public void OEsconderijoEOSonarSaemDaTabela()
    {
        var w = Build();
        Assert.True(w.ShipClasses["submarino"].IsSub);
        Assert.False(w.ShipClasses["destroier"].IsSub);
        Assert.True(w.ShipClasses["destroier"].IsHunter);
        Assert.False(w.ShipClasses["submarino"].IsHunter);
        Assert.Equal(0f, Subs.Stealth(w, ""), 3);                        // casco sem classe não se esconde
        Assert.Equal(0f, Subs.Asw(w, ""), 3);

        Assert.Equal(4f, Subs.Hulls(w, new Dictionary<string, float>
            { ["submarino"] = 4f, ["destroier"] = 9f, [""] = 2f }), 3);
        // à caça sai o casco com sonar e não o mais pesado: é o valor da tarefa que escolhe (Navy.Value)
        Assert.Equal(w.ShipClasses["destroier"].Asw, Navy.Value(w, "destroier", "asw"), 3);
        Assert.Equal("caca_submarina", Subs.MissionId(w));
        Assert.Equal("asw", w.NavalMissionDefs[Subs.MissionId(w)].Effect);
    }

    /// <summary>A conta do esconderijo: sem busca nenhuma não se vê nada, o bando dilui a busca (cobrir dez
    /// é mais difícil do que cobrir um) e sem submarinos vê-se tudo, que é o mundo de sempre.</summary>
    [Fact]
    public void OBandoDiluiABuscaEQuemNaoBuscaNaoVeNada()
    {
        var w = Build();
        Assert.Equal(0f, Subs.Seen(w, 0f, 4f), 3);
        Assert.Equal(1f, Subs.Seen(w, 10f, 0f), 3);
        float um = Subs.Seen(w, 12f, 1f), dez = Subs.Seen(w, 12f, 10f);
        Assert.True(um > dez, $"o bando não diluiu nada: {um} contra {dez}");
        Assert.InRange(Subs.Seen(w, 12f, 4f), 0f, 1f);

        // e a mesma conta vista do mar: a esquadra deles em cima do bando revela uma parte
        var deles = Esquadra(w, 2, Mar, "bloqueio", ("submarino", 6f));
        Assert.Equal(1f, Subs.HiddenShare(w, 2, Zones.Sea(w, Mar)), 3);   // sem ninguém a procurar
        Esquadra(w, 1, Mar, "patrulha", ("destroier", 10f));
        float escondido = Subs.HiddenShare(w, 2, Zones.Sea(w, Mar));
        Assert.True(escondido < 1f && escondido > 0f, $"busca passiva deu {escondido}");
        Assert.Equal(6f * (1f - escondido), Subs.Visible(w, 2, Zones.Sea(w, Mar)), 3);
        Assert.Equal(6f, Subs.Pack(w, 2, Zones.Sea(w, Mar)), 3);
        Assert.Equal(6f, deles.Ships, 3);
    }

    /// <summary>A missão de caça vê muito mais do que uma esquadra que anda noutra coisa: é para isso que
    /// ela existe (asw_passive contra o peso inteiro da missão).</summary>
    [Fact]
    public void ACacaVeMaisDoQueQuemAndaNoutraCoisa()
    {
        float Escondido(string missao)
        {
            var w = Build();
            Esquadra(w, 2, Mar, "bloqueio", ("submarino", 6f));
            Esquadra(w, 1, Mar, missao, ("destroier", 10f));
            return Subs.HiddenShare(w, 2, Zones.Sea(w, Mar));
        }

        float passiva = Escondido("patrulha"), caca = Escondido("caca_submarina");
        Assert.True(caca < passiva, $"a caça não viu mais: {caca} contra {passiva}");
    }

    /// <summary>O que está escondido não leva tiro no combate de esquadra — e também não o dá: quem se
    /// esconde não está na linha. É a regra inteira da guerra submarina.</summary>
    [Fact]
    public void OEscondidoNaoLevaTiroNoCombateDeEsquadra()
    {
        var w = Build();
        var deles = Esquadra(w, 2, Mar, "bloqueio", ("submarino", 6f));
        Esquadra(w, 1, Mar, "bloqueio", ("cruzador", 8f));   // peso de linha, sonar quase nenhum

        float escondido = Subs.HiddenShare(w, 2, Zones.Sea(w, Mar));
        Assert.True(escondido > 0.5f, $"o cruzador não devia ver o mar todo: {escondido}");
        float antes = deles.Ships;
        w.Tick();
        float perdido = antes - deles.Ships;
        Assert.True(perdido <= antes * (1f - escondido) + 0.001f,
                    $"afundaram-se {perdido} de {antes} com {escondido:P0} escondidos");
    }

    /// <summary>Ir buscar o que está lá em baixo é obra da caça: uma esquadra em patrulha revela-os (e o
    /// combate de esquadra ainda apanha os que ficaram à vista), mas nenhum submarino vai ao fundo por obra
    /// dela. Com a caça em cima, o bando encolhe muito mais depressa.</summary>
    [Fact]
    public void SoACacaVaiBuscarOQueEstaLaEmBaixo()
    {
        (float Cacados, float Perdidos) Fim(string missao)
        {
            var w = Build();
            var deles = Esquadra(w, 2, Mar, "bloqueio", ("submarino", 6f));
            Esquadra(w, 1, Mar, missao, ("destroier", 12f));
            float ido = 0f;
            w.Events.Subscribe<SubsHunted>(e => ido += e.Ships);
            w.Tick();
            return (ido, 6f - deles.Ships);
        }

        var vigia = Fim("patrulha");
        var caca = Fim("caca_submarina");
        Assert.Equal(0f, vigia.Cacados, 3);                      // ver o mar não é varrer o mar
        Assert.True(caca.Cacados > 0f, "a caça não afundou nada");
        Assert.True(caca.Perdidos > vigia.Perdidos,
                    $"o bando não encolheu mais com a caça: {caca.Perdidos} contra {vigia.Perdidos}");
    }

    /// <summary>A caça não apanha mais do que vê: contra o mesmo bando, poucos cascos afundam menos do que
    /// muitos — e não é só por serem menos, é por verem menos.</summary>
    [Fact]
    public void SoSeAfundaOQueSeVe()
    {
        float Afundado(float cascos)
        {
            var w = Build();
            Esquadra(w, 2, Mar, "bloqueio", ("submarino", 8f));
            Esquadra(w, 1, Mar, "caca_submarina", ("destroier", cascos));
            float ido = 0f;
            w.Events.Subscribe<SubsHunted>(e => ido += e.Ships);
            w.Tick();
            return ido;
        }

        float poucos = Afundado(4f), muitos = Afundado(20f);
        Assert.True(poucos > 0f && muitos > poucos, $"{poucos} contra {muitos}");
        Assert.True(muitos < 8f, "a caça de um dia não devia varrer o bando todo");
    }

    /// <summary>Os cascos que a caça leva do porto são os do sonar, e o que ela afunda sai do pool nacional
    /// deles — um submarino ao fundo é um submarino a menos no país, não só na esquadra.</summary>
    [Fact]
    public void ACacaLevaOSonarEOQueVaiAoFundoSaiDoPais()
    {
        var w = Build();
        Esquadra(w, 2, Mar, "bloqueio", ("submarino", 6f));
        var nossos = w.Countries[1];
        nossos.Ships["destroier"] = 6f; nossos.Ships["cruzador"] = 6f;

        NavalMissionSystem.Assign(w, 1, Mar, "caca_submarina", 6f);
        var caca = Assert.Single(w.NavalMissions, m => m.CountryId == 1);
        Assert.Equal(6f, caca.Squadron["destroier"], 3);                 // o sonar primeiro, o peso fica em casa
        Assert.False(caca.Squadron.ContainsKey("cruzador"));

        float antes = w.Countries[2].Ships["submarino"];
        w.Tick();
        Assert.True(w.Countries[2].Ships.GetValueOrDefault("submarino") < antes - 0.001f,
                    "o pool nacional deles não pagou a perda");
        Assert.True(w.Countries[1].NavyXp > 0f);                          // caçar ensina
    }

    /// <summary>Nem o céu chega ao que está escondido: o ataque naval afunda o que anda à superfície e o
    /// resto continua lá em baixo. Um submarino só se vai buscar com sonar.</summary>
    [Fact]
    public void NemOCeuAfundaOQueEstaEscondido()
    {
        var w = Build();
        TestWorld.Fields(w);
        w.Register(new AirMissionSystem());
        var c = w.Countries[1];
        c.Planes["ataque"] = 20f;
        var deles = Esquadra(w, 2, Mar, "bloqueio", ("submarino", 8f));
        AirMissionSystem.Assign(w, 1, 3, "ataque_naval", 6f);              // do nosso chão, sobre o mar deles

        float escondido = Subs.HiddenShare(w, 2, Zones.Sea(w, Mar));
        float ido = 0f;
        w.Events.Subscribe<AirNavalStrike>(e => ido += e.Ships);
        w.Tick();
        Assert.True(ido <= 8f * (1f - escondido) + 0.001f, $"o céu afundou {ido} com {escondido:P0} escondidos");
        Assert.True(deles.Ships >= 8f * escondido - 0.001f, "o escondido não sobreviveu");
    }

    /// <summary>A IA manda a caça quando lhe metem submarinos no mar de casa, e o resto da esquadra continua
    /// a ir bloquear — a caça não come a marinha toda.</summary>
    [Fact]
    public void AIaMandaACacaAoMarDeCasa()
    {
        var w = Build();
        var nos = w.Countries[1];
        nos.IsPlayer = false;
        nos.Ships["destroier"] = 20f;
        Esquadra(w, 2, 3, "bloqueio", ("submarino", 6f));                  // debaixo da nossa costa
        Assert.Equal(3, Subs.Prey(w, 1));

        w.Tick();
        var caca = w.NavalMissions.SingleOrDefault(m => m.CountryId == 1 && m.MissionId == "caca_submarina");
        Assert.NotNull(caca);
        Assert.Equal(3, caca!.RegionId);
        Assert.Contains(w.NavalMissions, m => m.CountryId == 1 && m.MissionId == "bloqueio");
    }
}
