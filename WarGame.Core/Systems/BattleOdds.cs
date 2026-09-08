using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A chapa da batalha: quem está a levar a melhor, e por quanto.
///
/// Uma batalha no mapa era duas espadas coladas ao número das divisões. Diziam que ali se combatia e mais
/// nada — para saber se se estava a ganhar era preciso abrir o painel da batalha, região a região. Num
/// mapa com dez frentes isso é o mesmo que não saber nada.
///
/// No HoI4 a batalha é um ponteiro com um número e uma cor: verde quando o nosso lado leva a melhor,
/// amarelo quando está renhida, vermelho quando estamos a perder. A cor lê-se sem contar nada, e é ela
/// que diz para onde mandar as reservas antes de a linha ceder.
///
/// A força de um lado é gente de pé pesada pela organização: uma divisão desfeita conta pouco mesmo com o
/// efectivo cheio, e uma divisão inteira mas exausta também. Os pesos vêm da regra battle_org_weight,
/// e a banda do amarelo da battle_even_band. Nada se guarda: a chapa é a conta do momento.</summary>
public static class BattleOdds
{
    /// <summary>A força de um lado da batalha: soma do que cada divisão ainda tem para dar, de 0 para cima.</summary>
    public static float Strength(World w, IEnumerable<int> divisionIds)
    {
        float org = w.Rule("battle_org_weight", 0.6f);
        float men = 1f - org;
        float sum = 0f;
        foreach (int id in divisionIds)
            if (w.Divisions.TryGetValue(id, out var d))
                sum += MathF.Max(0f, d.Org / 100f) * org + MathF.Max(0f, d.Hp / 100f) * men;
        return sum;
    }

    /// <summary>A vantagem do atacante, de -1 (a perder tudo) a +1 (a ganhar tudo). 0 quando não há ninguém
    /// de nenhum dos lados — uma batalha sem gente não pende para lado nenhum.</summary>
    public static float Tilt(World w, Battle b)
    {
        float a = Strength(w, b.Attackers), d = Strength(w, b.Defenders);
        return a + d <= 0.0001f ? 0f : (a - d) / (a + d);
    }

    /// <summary>A vantagem lida do lado de quem olha. Quem está a atacar lê o mesmo sinal; quem defende
    /// lê-o ao contrário; quem só vê a batalha de fora lê-a como ela está para o atacante.</summary>
    public static float Tilt(World w, Battle b, int viewerId)
    {
        float t = Tilt(w, b);
        if (viewerId <= 0) return t;
        bool attacking = b.AttackerCountryId == viewerId || w.SameFaction(viewerId, b.AttackerCountryId);
        if (attacking) return t;
        bool defending = b.Defenders.Any(id => w.Divisions.TryGetValue(id, out var d)
                                               && (d.CountryId == viewerId || w.SameFaction(viewerId, d.CountryId)));
        return defending ? -t : t;
    }

    /// <summary>O número que vai na chapa: a vantagem em partes de cem, de 0 a 99, sem sinal (o sinal é a cor).</summary>
    public static int Number(World w, Battle b, int viewerId) =>
        Math.Clamp((int)MathF.Round(MathF.Abs(Tilt(w, b, viewerId)) * 100f), 0, 99);

    /// <summary>A cor da chapa aos olhos de quem olha: 1 a ganhar (verde), 0 renhida (amarelo), -1 a perder
    /// (vermelho). A banda do amarelo é a regra battle_even_band.</summary>
    public static int Mood(World w, Battle b, int viewerId)
    {
        float t = Tilt(w, b, viewerId), band = MathF.Max(0f, w.Rule("battle_even_band", 0.12f));
        return t > band ? 1 : t < -band ? -1 : 0;
    }

    /// <summary>O que a chapa diz por extenso, para a ficha e para a prova headless.</summary>
    public static string Line(World w, Battle b, int viewerId)
    {
        string mood = Mood(w, b, viewerId) switch { 1 => "a ganhar", -1 => "a perder", _ => "renhida" };
        return $"{mood} por {Number(w, b, viewerId)}";
    }

    /// <summary>As batalhas que o jogador tem como ver, das mais renhidas para as mais decididas — no mapa
    /// interessa primeiro a que ainda pode virar. Batalha em terra sob nevoeiro não aparece.</summary>
    public static List<Battle> Shown(World w, int viewerId)
    {
        var list = new List<Battle>();
        foreach (var b in w.ActiveBattles)
        {
            if (!w.Regions.TryGetValue(b.RegionId, out var r)) continue;
            if (Vision.Enabled(w) && !Vision.Sees(w, viewerId, r)) continue;
            list.Add(b);
        }
        list.Sort((x, y) =>
        {
            int cmp = MathF.Abs(Tilt(w, x, viewerId)).CompareTo(MathF.Abs(Tilt(w, y, viewerId)));
            return cmp != 0 ? cmp : x.RegionId.CompareTo(y.RegionId);
        });
        return list;
    }
}
