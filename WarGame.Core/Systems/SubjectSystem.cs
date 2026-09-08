using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Estados-fantoche: o que fazer a um país que se venceu e não se quer engolir.
///
/// Ganhar uma guerra aqui era só tirar terra — anexava-se província a província, ou assinava-se e ficava
/// tudo como estava. No HoI4 a vitória tem outra forma, e é a que enche o mapa a meio da campanha: o
/// derrotado continua a existir, com bandeira, terra e exército, mas debaixo de nós. Paga-nos parte do que
/// rende e dá-nos parte dos homens que recruta; em troca vai ganhando autonomia, degrau a degrau, até ao dia
/// em que se levanta e sai. É a diferença entre um império que se administra e um mapa de uma cor só.
///
/// A tabela é que manda: os degraus (protectorado, satélite, domínio), o que cada um paga e a que velocidade
/// se solta vêm da tabela subject_type. O degrau NÃO se guarda no save — sai da autonomia, que é o único
/// número que o vassalo carrega (mais o suserano). Um degrau guardado podia contradizer a autonomia; assim
/// não pode.
///
/// Contrato: corre depois do rendimento e dos homens do dia (EconomySystem, ManpowerSystem) — o que sobe ao
/// suserano é uma fatia do dia que o vassalo acabou de ganhar, contada pelas mesmas funções públicas com que
/// eles o ganharam, e não uma segunda conta.</summary>
public sealed class SubjectSystem : ISystem
{
    public string Name => "Subjects";

    public void Tick(World w)
    {
        if (w.SubjectTypeDefs.Count == 0) return;
        float free = w.Rule("subject_free_autonomy", 1f);
        float atWar = w.Rule("subject_autonomy_war", 0.0035f);

        foreach (var c in w.Countries.Values)
        {
            if (!c.IsSubject) continue;
            if (!w.Countries.TryGetValue(c.OverlordId, out var lord) || lord.Id == c.Id) { c.OverlordId = 0; continue; }
            if (Subjects.Level(w, c) is not SubjectTypeDef lvl) continue;

            // o tributo do dia: uma fatia do que ele rendeu e dos homens que entraram
            float tribute = Subjects.Tribute(w, c);
            if (tribute > 0f) { c.Money = MathF.Max(0f, c.Money - tribute); lord.Money += tribute; }
            float levy = Subjects.Levy(w, c);
            if (levy > 0f) { c.Manpower = MathF.Max(0f, c.Manpower - levy); lord.Manpower += levy; }

            // e o preço de o ter: todos os dias ele fica um bocadinho mais seu. Quem se bate — pela causa
            // do suserano ou pela sua — cobra a conta mais depressa: é sangue que dá direito a voz.
            c.Autonomy += lvl.Drift + (c.AtWarWith.Count > 0 ? atWar : 0f);
            if (c.Autonomy < free) continue;

            c.Autonomy = 0f; c.OverlordId = 0;
            w.Events.Publish(new SubjectFreed(c.Id, lord.Id));
        }
    }
}

/// <summary>As contas da vassalagem, sem estado nenhum: em que degrau está um vassalo, o que ele paga hoje e
/// como se lê tudo isso numa linha. Quem as aplica é o SubjectSystem; quem as mostra é a UI, e é por aqui
/// que ela as mostra sem refazer nenhuma.</summary>
public static class Subjects
{
    /// <summary>O degrau em que este país está: o mais alto cuja autonomia mínima ele já passou. Null se não
    /// é vassalo de ninguém (ou se a tabela não veio carregada).</summary>
    public static SubjectTypeDef? Level(World w, Country c)
    {
        if (!c.IsSubject) return null;
        SubjectTypeDef? best = null;
        foreach (var t in w.SubjectTypeDefs.Values.OrderBy(t => t.AutonomyMin).ThenBy(t => t.Sort))
            if (c.Autonomy >= t.AutonomyMin) best = t;
        return best ?? w.SubjectTypeDefs.Values.OrderBy(t => t.AutonomyMin).FirstOrDefault();
    }

    /// <summary>Os pontos de produção que sobem hoje ao suserano: a fatia do degrau sobre o rendimento do dia
    /// do vassalo (a mesma conta do EconomySystem, chamada e não copiada). Com o degrau dado à mão responde
    /// a "quanto renderia se se curvasse hoje", sem se lhe tocar.</summary>
    public static float Tribute(World w, Country c, SubjectTypeDef t) =>
        MathF.Max(0f, EconomySystem.Income(w, c.Id) * t.YieldShare);

    public static float Tribute(World w, Country c) =>
        Level(w, c) is SubjectTypeDef t ? Tribute(w, c, t) : 0f;

    /// <summary>Os homens que sobem hoje ao suserano: a fatia do degrau sobre a leva do dia do vassalo (a
    /// mesma conta do ManpowerSystem).</summary>
    public static float Levy(World w, Country c, SubjectTypeDef t) =>
        MathF.Max(0f, ManpowerSystem.Gain(w, c, ManpowerSystem.Pop(w, c.Id)) * t.ManpowerShare);

    public static float Levy(World w, Country c) =>
        Level(w, c) is SubjectTypeDef t ? Levy(w, c, t) : 0f;

    /// <summary>O degrau mais fundo da tabela: aquele em que se entra quando alguém se curva.</summary>
    public static SubjectTypeDef? Deepest(World w) =>
        w.SubjectTypeDefs.Values.OrderBy(t => t.AutonomyMin).ThenBy(t => t.Sort).FirstOrDefault();

    /// <summary>Os vassalos de um país, por ordem de quem está mais preso.</summary>
    public static List<Country> Of(World w, int overlordId) =>
        w.Countries.Values.Where(c => c.OverlordId == overlordId).OrderBy(c => c.Autonomy).ThenBy(c => c.Id).ToList();

    /// <summary>Quantos dias faltam até este vassalo se levantar, ao ritmo de hoje. -1 quando não anda para
    /// lado nenhum (degrau sem deriva e sem guerra).</summary>
    public static int DaysToFreedom(World w, Country c)
    {
        if (Level(w, c) is not SubjectTypeDef t) return -1;
        float pace = t.Drift + (c.AtWarWith.Count > 0 ? w.Rule("subject_autonomy_war", 0.0035f) : 0f);
        if (pace <= 0f) return -1;
        return (int)MathF.Ceiling(MathF.Max(0f, w.Rule("subject_free_autonomy", 1f) - c.Autonomy) / pace);
    }

    /// <summary>A vassalagem numa linha, para a ficha do país e para o --smoke.</summary>
    public static string Line(World w, Country c)
    {
        if (Level(w, c) is not SubjectTypeDef t) return "";
        string lord = w.Countries.TryGetValue(c.OverlordId, out var o) ? o.Name : "—";
        float share = MathF.Max(0.0001f, w.Rule("subject_free_autonomy", 1f));
        int days = DaysToFreedom(w, c);
        return $"{t.Name} de {lord}: autonomia {c.Autonomy / share * 100f:0}%"
             + $" · paga {t.YieldShare * 100f:0}% do rendimento e {t.ManpowerShare * 100f:0}% dos homens"
             + (days < 0 ? "" : $" · livre em {days} dia{(days == 1 ? "" : "s")}");
    }

    /// <summary>Põe um país debaixo de outro. Um vassalo fica com a terra que é dele: o que estava ocupado
    /// volta ao dono, porque quem manda no país já não precisa de lhe ocupar as províncias.</summary>
    public static void Puppet(World w, int overlordId, int subjectId)
    {
        if (!w.Countries.TryGetValue(overlordId, out var lord) || !w.Countries.TryGetValue(subjectId, out var sub)) return;
        if (overlordId == subjectId) return;
        // ninguém é vassalo do seu próprio vassalo: quem sobe, sobe solto
        if (lord.OverlordId == subjectId) { lord.OverlordId = 0; lord.Autonomy = 0f; }
        sub.OverlordId = overlordId;
        sub.Autonomy = Math.Clamp(w.Rule("subject_autonomy_start", 0f), 0f, w.Rule("subject_free_autonomy", 1f));
        w.Events.Publish(new SubjectMade(subjectId, overlordId));
    }
}
