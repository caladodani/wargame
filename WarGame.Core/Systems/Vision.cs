using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Nevoeiro de guerra: quem vê o quê. Até aqui o mapa era uma mesa de xadrez com as peças todas
/// viradas para cima — o jogador contava as divisões do outro lado do continente sem lá ter ninguém, e
/// planeava contra números que nenhum estado-maior da época teria. A espionagem existia e não servia para
/// ver nada, porque já se via tudo.
///
/// Uma região está à vista quando há razão física para lá haver olhos nossos:
/// • é nossa (controlamos) ou temos lá tropa;
/// • é de um aliado da mesma facção — os aliados partilham o que vêem;
/// • faz fronteira (por terra ou por mar) com terreno nosso — as patrulhas de fronteira vêem o vizinho;
/// • temos rede de informações montada sobre quem a controla (EspionageSystem: World.HasIntel);
/// • temos uma operação a decorrer nessa própria região.
///
/// Fora disso vê-se o dono da terra, que é público, mas não a guarnição. A regra fog_of_war desliga tudo
/// (0 = mapa aberto), porque quem quiser o jogo antigo há-de poder tê-lo sem mexer em código.
///
/// Só faz contas sobre o World: não guarda estado, não publica eventos e não entra no save. Quem a usa é a
/// UI (marcadores do mapa, painel da região) — as decisões da IA continuam a correr sobre o mundo real,
/// que é o que faz sentido: cada IA sabe o que sabe da sua própria casa.</summary>
public static class Vision
{
    /// <summary>O nevoeiro está ligado? (regra fog_of_war; 0 = mapa aberto).</summary>
    public static bool Enabled(World w) => w.Rule("fog_of_war", 1f) > 0f;

    /// <summary>Vemos o que se passa dentro desta região?</summary>
    public static bool Sees(World w, int viewerId, Region r)
    {
        if (!Enabled(w) || viewerId <= 0) return true;
        if (r.ControllerId == viewerId || r.OwnerId == viewerId) return true;
        if (w.SameFaction(viewerId, r.ControllerId)) return true;

        // tropa nossa lá dentro (assalto a decorrer, passagem combinada): quem lá está, vê
        foreach (int id in r.DivisionIds)
            if (w.Divisions.TryGetValue(id, out var d) && (d.CountryId == viewerId || w.SameFaction(viewerId, d.CountryId)))
                return true;

        if (w.HasIntel(viewerId, r.ControllerId)) return true;
        foreach (var op in w.ActiveSpyOps)
            if (op.CountryId == viewerId && op.RegionId == r.Id) return true;

        return Adjacent(w, viewerId, r);
    }

    /// <summary>Atalho por id de região.</summary>
    public static bool Sees(World w, int viewerId, int regionId) =>
        !Enabled(w) || (w.Regions.TryGetValue(regionId, out var r) && Sees(w, viewerId, r));

    /// <summary>Faz fronteira com terreno nosso (ou de um aliado), por terra ou por mar?</summary>
    private static bool Adjacent(World w, int viewerId, Region r)
    {
        foreach (int n in r.Neighbours)
            if (Held(w, viewerId, n)) return true;
        foreach (int n in r.SeaNeighbours.Keys)
            if (Held(w, viewerId, n)) return true;
        return false;
    }

    private static bool Held(World w, int viewerId, int regionId) =>
        w.Regions.TryGetValue(regionId, out var n)
        && (n.ControllerId == viewerId || w.SameFaction(viewerId, n.ControllerId));

    /// <summary>Divisões desta região que o observador pode ver. As suas e as dos aliados vêem-se sempre;
    /// as dos outros só com a região à vista.</summary>
    public static IEnumerable<Division> DivisionsIn(World w, int viewerId, Region r)
    {
        bool open = Sees(w, viewerId, r);
        foreach (int id in r.DivisionIds)
            if (w.Divisions.TryGetValue(id, out var d)
                && (open || d.CountryId == viewerId || w.SameFaction(viewerId, d.CountryId)))
                yield return d;
    }

    /// <summary>Quantas divisões o observador conta nesta região (0 quando o nevoeiro a tapa).</summary>
    public static int CountIn(World w, int viewerId, Region r) => DivisionsIn(w, viewerId, r).Count();

    /// <summary>Porque é que esta região está à vista — uma frase curta para a UI explicar o nevoeiro em vez
    /// de simplesmente esconder números.</summary>
    public static string Why(World w, int viewerId, Region r)
    {
        if (!Enabled(w)) return "mapa aberto";
        if (r.ControllerId == viewerId || r.OwnerId == viewerId) return "terreno nosso";
        if (w.SameFaction(viewerId, r.ControllerId)) return "aliado: partilha o que vê";
        foreach (int id in r.DivisionIds)
            if (w.Divisions.TryGetValue(id, out var d) && (d.CountryId == viewerId || w.SameFaction(viewerId, d.CountryId)))
                return "temos lá tropa";
        if (w.HasIntel(viewerId, r.ControllerId)) return "rede de informações montada";
        foreach (var op in w.ActiveSpyOps)
            if (op.CountryId == viewerId && op.RegionId == r.Id) return "operação a decorrer no terreno";
        if (Adjacent(w, viewerId, r)) return "fronteira à vista das nossas patrulhas";
        return "sem olhos nossos: só se sabe de quem é a terra";
    }
}
