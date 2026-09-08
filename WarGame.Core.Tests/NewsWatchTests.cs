using WarGame.Core.Commands;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Eventos que o mundo faz acontecer: sem data marcada, com uma sonda à espera. É a diferença
/// entre um calendário que se vê chegar e um mundo que reage ao que nos está a acontecer.</summary>
public class NewsWatchTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.NewsEvents.Clear();                 // os eventos semeados têm prova própria; aqui monta-se o caso
        w.NewsEffects.Clear();
        w.NewsOptions.Clear();
        w.NewsOptionEffects.Clear();
        w.Countries[1].IsPlayer = true;
        w.Rules["event_watch_day"] = 0f;      // a semana de descanso tem prova própria mais abaixo
        return w;
    }

    private static NewsEvent Watch(string id, string watch, float arg = 0f) =>
        new(id, 0, null, "Título", "corpo", watch, arg, "espadas", "mau", true);

    /// <summary>O evento espera pelo mundo: só cai no dia em que a sonda acende, e fica escrito em cima
    /// de quem caiu — que num evento sem tag é quem está a jogar.</summary>
    [Fact]
    public void O_evento_cai_no_dia_em_que_o_mundo_muda()
    {
        var w = Setup();
        w.NewsEvents["guerra"] = Watch("guerra", "guerra", 1f);
        var sys = new NewsSystem();
        w.Clock.Advance(); sys.Tick(w);
        Assert.Empty(w.NewsFired);

        w.StartWar(1, 2);
        w.Clock.Advance(); sys.Tick(w);
        Assert.True(w.NewsFired.ContainsKey("guerra"));
        Assert.Equal(1, w.NewsFired["guerra"].CountryId);
        Assert.Equal(w.Clock.Day, w.NewsFired["guerra"].Day);
    }

    /// <summary>Cada evento cai uma vez. A guerra continua no dia seguinte e no outro, e o cartão não
    /// volta a abrir — senão o mundo passava a ser uma campainha.</summary>
    [Fact]
    public void Cada_evento_cai_uma_vez_so()
    {
        var w = Setup();
        w.NewsEvents["guerra"] = Watch("guerra", "guerra", 1f);
        var fired = new List<NewsFired>();
        w.Events.Subscribe<NewsFired>(fired.Add);
        var sys = new NewsSystem();
        w.StartWar(1, 2);
        for (int i = 0; i < 5; i++) { w.Clock.Advance(); sys.Tick(w); }
        Assert.Single(fired);
        int day = w.NewsFired["guerra"].Day;
        w.Clock.Advance(); sys.Tick(w);
        Assert.Equal(day, w.NewsFired["guerra"].Day);
    }

    /// <summary>Sem ninguém a jogar (mundo só de IA) um evento de estado sem tag não cai em ninguém: não
    /// há secretária onde pousar a notícia.</summary>
    [Fact]
    public void Sem_jogador_o_evento_sem_tag_nao_cai()
    {
        var w = Setup();
        w.Countries[1].IsPlayer = false;
        w.NewsEvents["guerra"] = Watch("guerra", "guerra", 1f);
        w.StartWar(1, 2);
        w.Clock.Advance(); new NewsSystem().Tick(w);
        Assert.Empty(w.NewsFired);
    }

    /// <summary>Com tag, o evento é do país da tag, jogue-o quem o jogar — e o efeito não passa ao vizinho.</summary>
    [Fact]
    public void Com_tag_o_evento_e_daquele_pais()
    {
        var w = Setup();
        w.NewsEvents["fome"] = Watch("fome", "guerra", 1f) with { CountryId = 2 };
        w.NewsEffects["fome"] = new() { ("industry", 0.9f) };
        w.StartWar(1, 2);
        w.Clock.Advance(); new NewsSystem().Tick(w);
        Assert.Equal(2, w.NewsFired["fome"].CountryId);
        Assert.Equal(0.9f, w.Countries[2].Stat("industry"), 0.001f);
        Assert.Equal(1f, w.Countries[1].Stat("industry"), 0.001f);
    }

    /// <summary>O efeito só conta depois de o evento cair — antes disso o mundo não sabe dele — e conta
    /// outra vez a seguir a um recálculo, que é o que acontece quando se recarrega um save.</summary>
    [Fact]
    public void O_efeito_so_conta_depois_de_cair_e_aguenta_o_recalculo()
    {
        var w = Setup();
        w.NewsEvents["avanco"] = Watch("avanco", "terra_tomada", 2f);
        w.NewsEffects["avanco"] = new() { ("production_speed", 1.5f) };
        w.ApplyTechs(w.Countries[1]);
        Assert.Equal(1f, w.Countries[1].Stat("production_speed"), 0.001f);

        w.Regions[4].ControllerId = 1;
        w.Regions[5].ControllerId = 1;
        w.Clock.Advance(); new NewsSystem().Tick(w);
        Assert.Equal(1.5f, w.Countries[1].Stat("production_speed"), 0.001f);
        w.ApplyTechs(w.Countries[1]);
        Assert.Equal(1.5f, w.Countries[1].Stat("production_speed"), 0.001f);
    }

    /// <summary>Um evento de estado com escolhas pára à espera do jogador (NewsChoiceRequired) e não
    /// escolhe por ele.</summary>
    [Fact]
    public void O_evento_com_escolhas_espera_pelo_jogador()
    {
        var w = Setup();
        w.NewsEvents["gabinete"] = Watch("gabinete", "guerra", 1f);
        w.NewsOptions["gabinete"] = new() { new NewsOption("a", "gabinete", "Guerra total", 0), new NewsOption("b", "gabinete", "Vida civil", 1) };
        w.NewsOptionEffects["a"] = new() { ("industry", 1.2f) };
        var asked = new List<NewsChoiceRequired>();
        w.Events.Subscribe<NewsChoiceRequired>(asked.Add);
        w.StartWar(1, 2);
        w.Clock.Advance(); new NewsSystem().Tick(w);
        Assert.Single(asked);
        Assert.False(w.NewsChoices.ContainsKey("gabinete"));

        Assert.Null(new ChooseNewsOptionCommand(1, "gabinete", "a").Validate(w));
        new ChooseNewsOptionCommand(1, "gabinete", "a").Execute(w);
        Assert.Equal(1.2f, w.Countries[1].Stat("industry"), 0.001f);
    }

    /// <summary>Antes de cair não se escolhe nada: um evento de estado não tem dono nem opções em cima da
    /// mesa enquanto o mundo não o fizer acontecer.</summary>
    [Fact]
    public void Antes_de_cair_nao_ha_escolha_nenhuma()
    {
        var w = Setup();
        w.NewsEvents["gabinete"] = Watch("gabinete", "guerra", 1f);
        w.NewsOptions["gabinete"] = new() { new NewsOption("a", "gabinete", "Guerra total", 0) };
        Assert.NotNull(new ChooseNewsOptionCommand(1, "gabinete", "a").Validate(w));
    }

    /// <summary>Quem não joga fica com a primeira opção na hora, como sempre foi — o gabinete da IA não
    /// deixa uma decisão em cima da mesa.</summary>
    [Fact]
    public void A_IA_fica_com_a_primeira_opcao()
    {
        var w = Setup();
        w.NewsEvents["reforma"] = Watch("reforma", "guerra", 1f) with { CountryId = 2 };
        w.NewsOptions["reforma"] = new() { new NewsOption("a", "reforma", "Produção", 0), new NewsOption("b", "reforma", "Treino", 1) };
        w.NewsOptionEffects["a"] = new() { ("production_speed", 1.3f) };
        w.StartWar(1, 2);
        w.Clock.Advance(); new NewsSystem().Tick(w);
        Assert.Equal("a", w.NewsChoices["reforma"]);
        Assert.Equal(1.3f, w.Countries[2].Stat("production_speed"), 0.001f);
    }

    /// <summary>O mundo dá uma semana de descanso: no arranque metade das sondas está acesa por o país
    /// ainda não ter feito nada, e um cartão em cima do primeiro dia não é acontecimento nenhum.</summary>
    [Fact]
    public void A_semana_de_descanso_cala_o_arranque()
    {
        var w = Setup();
        w.Rules["event_watch_day"] = 7f;
        w.NewsEvents["guerra"] = Watch("guerra", "guerra", 1f);
        var sys = new NewsSystem();
        w.StartWar(1, 2);
        for (int i = 0; i < 6; i++) { w.Clock.Advance(); sys.Tick(w); }
        Assert.Empty(w.NewsFired);
        w.Clock.Advance(); sys.Tick(w);
        Assert.True(w.NewsFired.ContainsKey("guerra"));
    }

    /// <summary>O evento de data marcada continua a cair no dia — os dois feitios vivem na mesma tabela.</summary>
    [Fact]
    public void O_evento_de_calendario_continua_a_cair_no_dia()
    {
        var w = Setup();
        w.NewsEvents["cimeira"] = new NewsEvent("cimeira", 2, null, "Cimeira", "corpo");
        var sys = new NewsSystem();
        w.Clock.Advance(); sys.Tick(w);
        Assert.Empty(w.NewsFired);
        w.Clock.Advance(); sys.Tick(w);
        Assert.Equal(0, w.NewsFired["cimeira"].CountryId);       // 0 = o mundo inteiro
    }
}
