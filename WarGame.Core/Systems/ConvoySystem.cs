using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Comboios mercantes (HoI4: convoys). O bloqueio naval já fechava o cais, mas o mar continuava a
/// ser de graça para quem lá não tinha esquadra nenhuma: comprava-se aço do outro lado do mundo e
/// alimentava-se um exército numa ilha sem um único navio mercante. Faltava a marinha que ninguém desenha
/// nos mapas e sem a qual não há império nenhum.
///
/// Cada país tem uma marinha mercante de partida (convoy_base) e pode mandar construir mais
/// (BuyConvoyCommand, que soma a Country.Convoys — o saldo por cima dessa marinha, que fica negativo quando
/// a guerra ao comércio afunda mais do que aquilo que se construiu). O que ela carrega, por ordem:
/// 1. o abastecimento por mar do exército (convoy_per_sea_division por divisão que só bebe por mar);
/// 2. os tratados de comércio (convoy_per_trade_unit por unidade importada).
/// O que não couber fica em terra: as divisões do outro lado do mar recebem a fracção que os comboios
/// aguentam, e os contratos que sobram não entregam nem se pagam nesse dia — sem afundar o contrato, que a
/// falta é de navios e não de vontade.
///
/// A guerra ao comércio é o outro lado do bloqueio: onde uma esquadra fecha uma costa, afunda
/// convoy_raid_sink mercantes por navio e por dia a quem manda naquela terra. Bloquear deixa de ser só
/// fechar um cais — é ir comendo a marinha mercante do inimigo até ele não conseguir mover nada por mar.
///
/// Corre a seguir às missões navais e antes do abastecimento: os mercantes que foram ao fundo hoje já
/// faltam à fome de hoje.</summary>
public sealed class ConvoySystem : ISystem
{
    public string Name => "Convoys";

    public void Tick(World w)
    {
        if (w.NavalMissions.Count == 0) return;
        float sink = w.Rule("convoy_raid_sink", 0.25f);
        if (sink <= 0f) return;
        float floor = -w.Rule("convoy_base", 20f);

        foreach (var m in w.NavalMissions.OrderBy(x => x.CountryId).ThenBy(x => x.RegionId))
        {
            if (!w.NavalMissionDefs.TryGetValue(m.MissionId, out var def) || def.Effect != "blockade") continue;
            if (!w.Regions.TryGetValue(m.RegionId, out var r)) continue;
            if (!NavalMissionSystem.Blockaded(w, r.Id)) continue;             // escoltado: os mercantes passam
            if (!w.Countries.TryGetValue(r.ControllerId, out var victim)) continue;
            victim.Convoys = MathF.Max(floor, victim.Convoys - m.Ships * sink);
        }
    }

    /// <summary>Mercantes que este país tem hoje: a marinha de partida mais o saldo construído (ou menos o
    /// que a guerra ao comércio já lhe afundou). Nunca abaixo de zero.</summary>
    public static float Available(World w, int countryId) =>
        w.Countries.TryGetValue(countryId, out var c) ? MathF.Max(0f, w.Rule("convoy_base", 20f) + c.Convoys) : 0f;

    /// <summary>Mercantes presos ao abastecimento por mar do exército (as divisões que só bebem por mar).</summary>
    public static float SupplyNeed(World w, int countryId) =>
        Need(w, w.Countries.TryGetValue(countryId, out var c) ? c.SeaSupplied : 0);

    /// <summary>Mercantes que n divisões abastecidas por mar ocupam.</summary>
    public static float Need(World w, int divisions) => divisions * w.Rule("convoy_per_sea_division", 1f);

    /// <summary>Mercantes que os tratados de compra deste país ocupam.</summary>
    public static float TradeNeed(World w, int countryId) =>
        w.TradeDeals.Where(d => d.BuyerId == countryId).Sum(d => d.Units) * w.Rule("convoy_per_trade_unit", 2f);

    /// <summary>Fracção do abastecimento por mar que os mercantes deste país aguentam (0..1): 1 quando há
    /// navios para tudo. É o que o SupplySystem multiplica ao estrangulamento do cais.</summary>
    public static float Coverage(World w, int countryId, int divisions)
    {
        float need = Need(w, divisions);
        if (need <= 0f) return 1f;
        return Math.Clamp(Available(w, countryId) / need, 0f, 1f);
    }

    /// <summary>Este tratado ficou hoje sem navios que o carreguem? Os contratos do comprador enchem os
    /// mercantes por ordem fixa (vendedor, recurso) depois de o exército levar os dele — os que não couberem
    /// não entregam nem se pagam nesse dia.</summary>
    public static bool Grounded(World w, TradeDeal deal)
    {
        float left = Available(w, deal.BuyerId) - SupplyNeed(w, deal.BuyerId);
        float per = w.Rule("convoy_per_trade_unit", 2f);
        foreach (var d in w.TradeDeals.Where(x => x.BuyerId == deal.BuyerId)
                     .OrderBy(x => x.SellerId).ThenBy(x => x.ResourceId, StringComparer.Ordinal))
        {
            left -= d.Units * per;
            if (ReferenceEquals(d, deal)) return left < -0.001f;
        }
        return false;
    }

    /// <summary>Tratados deste comprador parados hoje por falta de mercantes (o painel mostra-os).</summary>
    public static int GroundedCount(World w, int countryId) =>
        w.TradeDeals.Count(d => d.BuyerId == countryId && Grounded(w, d));

    /// <summary>As travessias que os mercantes deste país fazem hoje, para o mapa as desenhar: as que
    /// alimentam o exército do outro lado do mar (um cais nosso até à costa nossa que ele alcança) e as que
    /// trazem o que se comprou (a costa do vendedor até à nossa). Ordem fixa: abastecimento primeiro, e
    /// dentro de cada tipo por região, para o desenho não bailar de tick para tick.</summary>
    public static List<ConvoyRoute> Routes(World w, int countryId)
    {
        var list = new List<ConvoyRoute>();
        if (!w.Countries.ContainsKey(countryId)) return list;
        var seen = new HashSet<(int, int)>();
        float perDiv = w.Rule("convoy_per_sea_division", 1f), perUnit = w.Rule("convoy_per_trade_unit", 2f);

        void Add(int from, int to, bool trade, float holds, bool blocked)
        {
            if (from == to || !seen.Add((Math.Min(from, to), Math.Max(from, to)))) return;
            list.Add(new ConvoyRoute(from, to, trade, holds, blocked));
        }

        // 1. abastecimento: cais nosso → costa nossa dentro do alcance dele. É a travessia que o
        // SupplySystem usa; a carga é a tropa que está do outro lado, que é quem bebe por mar.
        foreach (var port in w.Regions.Values.Where(r => r.ControllerId == countryId && r.Buildings.Count > 0)
                     .OrderBy(r => r.Id))
        {
            float reach = port.Buildings.Sum(kv => w.BuildingDefs.TryGetValue(kv.Key, out var d) ? d.SupplyRange * kv.Value : 0f);
            if (reach <= 0f) continue;
            foreach (var (dst, km) in port.SeaNeighbours.OrderBy(kv => kv.Key))
            {
                if (km > reach || !w.Regions.TryGetValue(dst, out var far) || far.ControllerId != countryId) continue;
                float troops = w.Divisions.Values.Count(d => d.RegionId == dst) * perDiv;
                Add(port.Id, dst, false, troops,
                    NavalMissionSystem.Blockaded(w, port.Id) || NavalMissionSystem.Blockaded(w, dst));
            }
        }

        // 2. comércio: a travessia mais curta entre a costa de quem vende e a nossa. Sem mar entre os dois
        // não há rota nenhuma que desenhar — a mercadoria vai por terra e não ocupa comboio à vista.
        foreach (var deal in w.TradeDeals.Where(d => d.BuyerId == countryId)
                     .OrderBy(d => d.SellerId).ThenBy(d => d.ResourceId, StringComparer.Ordinal))
        {
            int from = -1, to = -1; float best = float.MaxValue;
            foreach (var mine in w.Regions.Values.Where(r => r.ControllerId == countryId && r.SeaNeighbours.Count > 0))
                foreach (var (dst, km) in mine.SeaNeighbours)
                    if (km < best && w.Regions.TryGetValue(dst, out var far) && far.ControllerId == deal.SellerId)
                    { best = km; from = dst; to = mine.Id; }
            if (from < 0) continue;
            Add(from, to, true, deal.Units * perUnit,
                Grounded(w, deal) || NavalMissionSystem.Blockaded(w, from) || NavalMissionSystem.Blockaded(w, to));
        }
        return list;
    }
}

/// <summary>Uma travessia de comboios pronta a desenhar: de onde a que região, se leva comércio (senão é
/// abastecimento do exército), quantos mercantes ocupa e se hoje está cortada — por bloqueio no mar ou por
/// não haver navios que a façam.</summary>
public readonly record struct ConvoyRoute(int FromId, int ToId, bool Trade, float Holds, bool Blocked);
