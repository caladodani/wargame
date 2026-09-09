using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A mobília do mapa: as chapas que se põem por cima da terra, como no HoI4.
///
/// Até aqui tudo o que o mapa tinha a dizer sobre uma região saía na mesma pastilha de texto — a coroa da
/// capital, o número dos pontos, o quadrado do forte, a âncora do porto e as espadas da batalha, tudo em
/// fila num rótulo só. Lia-se de perto e não se lia de longe, e uma praça inimiga era igual a uma nossa:
/// letra branca em pastilha da cor do dono, que é a cor que já está pintada por baixo.
///
/// No HoI4 estas chapas são objectos de mapa e dizem duas coisas ao mesmo tempo sem se ler nada: a FORMA
/// diz o grau da praça (estrela para a capital, pentágono, quadrado, círculo à medida que vale menos) e a
/// COR diz de quem é — verde a nossa e a dos aliados, cinzenta a de terceiros, vermelha a de quem está em
/// guerra connosco. É por isto que uma frente se lê de relance: as chapas vermelhas do outro lado da linha
/// são exactamente os sítios para onde vale a pena mandar tropa.
///
/// Nada disto se guarda. O grau vem do VictoryPoints, a forma vem da linha victory_tier e a cor vem da
/// relação do momento — muda de cor sozinha no dia em que a guerra começa ou a praça troca de dono.</summary>
public static class MapMarks
{
    /// <summary>De quem é esta terra, aos olhos de quem olha: 1 nossa ou de aliado, -1 de inimigo em guerra
    /// connosco, 0 de terceiros. É o verde/vermelho/cinzento do mapa do HoI4.</summary>
    public static int Side(World w, int viewerId, int countryId)
    {
        if (countryId <= 0) return 0;
        if (countryId == viewerId || w.SameFaction(viewerId, countryId)) return 1;
        return w.AreAtWar(viewerId, countryId) ? -1 : 0;
    }

    /// <summary>Uma chapa de praça: onde fica, que forma leva, quantos pontos vale e de que lado está.
    /// Capital marca a chapa que é cadeira de governo — é a que leva estrela e nunca desaparece.</summary>
    public readonly record struct Prize(int RegionId, string Name, float X, float Y, string Shape,
                                        int Points, int Side, bool Capital, bool Seen);

    /// <summary>Quantos pontos uma praça tem de valer para se marcar a esta escala. Ao afastar sobe: ao
    /// longe só as capitais e as metrópoles, ao perto tudo o que conta. Conta pura, sem mundo — é a que o
    /// mapa corre a cada beliscão, com o mundo a andar ao lado.</summary>
    public static int Cut(int floor, float zoom) =>
        zoom >= 1f ? 1 : (int)MathF.Ceiling(Math.Max(1, floor) / MathF.Max(0.05f, zoom * 2f));

    /// <summary>O mesmo corte, com o chão vindo da regra victory_marker_min.</summary>
    public static int Cut(World w, float zoom) => Cut((int)w.Rule("victory_marker_min", 5f), zoom);

    /// <summary>Mostra-se esta praça a esta escala? A capital de um país vivo vê-se sempre que as chapas
    /// estão acesas — é o ponto que se procura primeiro num mapa de guerra.</summary>
    public static bool Shows(World w, Region r, float zoom)
    {
        if (zoom < w.Rule("mark_prize_zoom", 0.16f)) return false;
        int vp = VictoryPoints.Of(w, r);
        return vp > 0 && (IsCapital(w, r) || vp >= Cut(w, zoom));
    }

    /// <summary>A chapa leva o nome da praça escrito ao lado a partir desta escala (regra mark_name_zoom).</summary>
    public static bool Named(World w, Region r, float zoom) =>
        Shows(w, r, zoom) && zoom >= w.Rule("mark_name_zoom", 0.85f);

    /// <summary>Todas as praças do mundo que valem alguma coisa, tal como estão hoje — com o mundo parado.
    /// É esta a lista que o mapa guarda; o zoom só escolhe quais delas se desenham.</summary>
    public static List<Prize> All(World w, int viewerId)
    {
        var all = new List<Prize>();
        foreach (var r in w.Regions.Values)
        {
            int vp = VictoryPoints.Of(w, r);
            if (vp <= 0) continue;
            var tier = VictoryPoints.Tier(w, r);
            bool seen = !Vision.Enabled(w) || Vision.Sees(w, viewerId, r);
            all.Add(new Prize(r.Id, r.Name, r.CenterX, r.CenterY, tier?.Shape ?? "circulo",
                              vp, seen ? Side(w, viewerId, r.ControllerId) : 0, IsCapital(w, r), seen));
        }
        all.Sort((a, b) => a.Points != b.Points ? b.Points.CompareTo(a.Points) : a.RegionId.CompareTo(b.RegionId));
        return all;
    }

    /// <summary>As chapas a desenhar a esta escala, das que mais valem para as que menos valem, com tecto.
    /// A ordem é a que manda no tecto: cortar pelo fim nunca tira uma capital nem uma metrópole.</summary>
    public static List<Prize> Shown(List<Prize> all, int floor, float zoom, int max)
    {
        int cut = Cut(floor, zoom);
        var list = new List<Prize>();
        foreach (var p in all)
        {
            if (!p.Capital && p.Points < cut) continue;
            list.Add(p);
            if (list.Count >= Math.Max(1, max)) break;
        }
        return list;
    }

    /// <summary>As chapas a desenhar, indo buscar tudo ao mundo. Atalho para os testes e para a prova.</summary>
    public static List<Prize> Prizes(World w, int viewerId, float zoom) =>
        zoom < w.Rule("mark_prize_zoom", 0.16f) ? new List<Prize>()
        : Shown(All(w, viewerId), (int)w.Rule("victory_marker_min", 5f), zoom, (int)w.Rule("mark_draw_max", 600f));

    /// <summary>Cadeira de governo de um país que ainda está de pé.</summary>
    public static bool IsCapital(World w, Region r) =>
        w.Countries.TryGetValue(r.OwnerId, out var c) && !c.Capitulated && c.CapitalRegionId == r.Id;

    /// <summary>Uma obra que se vê no mapa: porto (âncora) ou forte (muro), com o nível dela.</summary>
    public readonly record struct Work(int RegionId, float X, float Y, string Glyph, int Level, int Side);

    /// <summary>Portos e fortes que há para desenhar. Obra que está no nevoeiro não entra: um forte inimigo
    /// por descobrir é precisamente a surpresa que o nevoeiro serve para guardar.</summary>
    public static List<Work> Works(World w, int viewerId)
    {
        var list = new List<Work>();
        // portos, depósitos e campos de aviação: as obras que se vêem do céu. Um depósito é uma chapa como o
        // porto porque faz o mesmo trabalho — é onde a rede começa outra vez —, e o campo de aviação entra
        // pela mesma razão: quem olha para o mapa tem de saber de onde é que a aviação do outro levanta
        var ports = w.BuildingDefs.Values.Where(d => d.SupplyRange > 0f || d.IsHub || d.IsAirfield)
                     .ToDictionary(d => d.Id, d => d.Glyph);
        foreach (var r in w.Regions.Values)
        {
            if (Vision.Enabled(w) && !Vision.Sees(w, viewerId, r)) continue;
            int side = Side(w, viewerId, r.ControllerId);
            foreach (var (id, level) in r.Buildings)
                if (level > 0 && ports.TryGetValue(id, out var g))
                    list.Add(new Work(r.Id, r.CenterX, r.CenterY, string.IsNullOrEmpty(g) ? "ancora" : g, level, side));
            if (r.Fort > 0) list.Add(new Work(r.Id, r.CenterX, r.CenterY, "muro", r.Fort, side));
        }
        list.Sort((a, b) => a.RegionId != b.RegionId ? a.RegionId.CompareTo(b.RegionId)
                                                     : string.CompareOrdinal(a.Glyph, b.Glyph));
        return list;
    }

    /// <summary>O que a chapa desta praça diz por extenso, para a ficha da região e para a prova headless.</summary>
    public static string Line(World w, int viewerId, Region r)
    {
        if (VictoryPoints.Of(w, r) <= 0) return "terra sem prémio";
        var tier = VictoryPoints.Tier(w, r);
        string side = Side(w, viewerId, r.ControllerId) switch
        {
            1 => "nossa", -1 => "do inimigo", _ => "de terceiros"
        };
        return $"{tier?.Name ?? "Praça"} {side}, {VictoryPoints.Of(w, r)} pontos";
    }
}
