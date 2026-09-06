using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>As estações do ano. O calendário andava e não mudava nada: Janeiro e Julho custavam o mesmo à
/// tropa. Agora o Inverno atola as colunas (World.SeasonMove), estraga a recomposição (World.SeasonOrg) e
/// gasta organização a quem passa a estação em campo aberto — sem um tiro disparado.
///
/// O desgaste é maior no terreno que a estação castiga (tabela season_terrain) e menor em
/// terreno próprio, onde há telhado e depósitos (season_shelter). Quem está em batalha já paga o combate e
/// fica de fora. Tudo — meses, multiplicadores e desgaste — vem das tabelas season e season_month.</summary>
public sealed class WeatherSystem : ISystem
{
    public string Name => "Weather";
    private string? _last;

    public void Tick(World w)
    {
        var season = w.Season;
        if (season is null) return;
        if (season.Id != _last)
        {
            // primeira volta de um jogo carregado também anuncia: o jogador tem de saber em que estação entra
            _last = season.Id;
            w.Events.Publish(new SeasonChanged(season.Id, season.Name, w.Clock.Day));
        }
        if (season.Attrition <= 0f) return;

        float shelter = w.Rule("season_shelter", 0.4f);
        var inBattle = new HashSet<int>(w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders)));
        foreach (var d in w.Divisions.Values)
        {
            if (inBattle.Contains(d.Id)) continue;
            var reg = w.Regions[d.RegionId];
            float bite = season.Attrition * w.SeasonBite(season.Id, reg.Terrain);
            if (reg.ControllerId == d.CountryId) bite *= shelter;      // em casa há telhado
            if (bite <= 0f) continue;
            d.Org = MathF.Max(0f, d.Org - bite);
        }
    }

    /// <summary>Quanto a estação castiga este terreno, já com a estação de hoje aplicada — a UI usa isto para
    /// dizer numa linha o que a região vai custar à tropa.</summary>
    public static float BiteOn(World w, string terrain) =>
        w.Season is SeasonDef s ? s.Attrition * w.SeasonBite(s.Id, terrain) : 0f;
}
