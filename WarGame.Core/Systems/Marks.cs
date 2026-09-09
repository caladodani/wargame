using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>As marcas do material (HoI4: Infantry Equipment I / II / III).
///
/// O armazém deste jogo tinha uma espingarda só. Um conjunto de material valia sempre o mesmo, viesse ele
/// da primeira fábrica ou de trinta anos de investigação, e por isso o ramo da infantaria era uma linha de
/// texto na árvore das tecnologias e mais nada. É a lacuna maior do ciclo produção→investigação→combate:
/// no HoI4 o que se investiga sai da fábrica e chega às mãos de quem se bate, com uma geração de atraso.
///
/// Aqui está essa cadeia: a tecnologia abre a marca (equipment_mark.tech_id), a linha de produção reafina-se
/// para a fazer — e perde ritmo na mudança, como uma linha de montagem a trocar de ferramenta —, a
/// prateleira do armazém guarda a marca MÉDIA do que lá está (é uma pilha misturada, não um catálogo), e a
/// divisão só sobe de marca quando recebe reforços dessa pilha. A tropa da frente anda sempre atrás do
/// laboratório, e isso vê-se no combate: Power multiplica a força de quem leva material novo.
///
/// A marca é um número com casas decimais de propósito: uma divisão a meio caminho entre a Mk II e a Mk III
/// bate-se a meio caminho das duas, e a conta interpola entre as linhas da tabela.
///
/// Estado derivado: não guarda nada de seu, não entra no save e não é ISystem — o que é guardado (a marca
/// da prateleira, a da divisão e a da linha) vive nas entidades que já existiam.</summary>
public static class Marks
{
    /// <summary>As marcas de um tipo de material, da primeira geração para a última.</summary>
    public static List<EquipmentMarkDef> All(World w, int unitTypeId) =>
        w.EquipmentMarks.Values.Where(m => m.UnitTypeId == unitTypeId)
                        .OrderBy(m => m.Mark).ThenBy(m => m.Id, StringComparer.Ordinal).ToList();

    /// <summary>A melhor marca deste tipo que o país já tem aberta: a última cuja tecnologia ele investigou
    /// (marca sem tecnologia é de origem e está sempre aberta). Zero num mundo sem tabela de marcas.</summary>
    public static float Open(World w, Country c, int unitTypeId)
    {
        float best = 0f;
        foreach (var m in All(w, unitTypeId))
            if (m.TechId.Length == 0 || c.Techs.Contains(m.TechId)) best = MathF.Max(best, m.Mark);
        return best;
    }

    /// <summary>Um número da tabela lido a uma marca com casas decimais: interpola entre a geração de baixo
    /// e a de cima. Fora da tabela (ou num mundo sem marcas) vale 1 — é o jogo de sempre.</summary>
    public static float Value(World w, int unitTypeId, float mark, string field)
    {
        var list = All(w, unitTypeId);
        if (list.Count == 0) return 1f;
        if (mark <= list[0].Mark) return Field(list[0], field);
        var last = list[^1];
        if (mark >= last.Mark) return Field(last, field);
        for (int i = 1; i < list.Count; i++)
        {
            var hi = list[i];
            if (mark > hi.Mark) continue;
            var lo = list[i - 1];
            float span = hi.Mark - lo.Mark;
            float t = span <= 0f ? 1f : (mark - lo.Mark) / span;
            return Field(lo, field) + t * (Field(hi, field) - Field(lo, field));
        }
        return Field(last, field);
    }

    private static float Field(EquipmentMarkDef d, string field) => field switch
    {
        "cost" => d.Cost, "power" => d.Power, "wear" => d.Wear, _ => 1f,
    };

    /// <summary>O que custa fabricar um conjunto desta marca, em multiplicadores do preço de origem.</summary>
    public static float Cost(World w, int unitTypeId, float mark) => Value(w, unitTypeId, mark, "cost");

    /// <summary>O que este material vale em combate.</summary>
    public static float Power(World w, int unitTypeId, float mark) => Value(w, unitTypeId, mark, "power");

    /// <summary>Quanto se gasta este material: abaixo de 1 é equipamento que aguenta mais guerra.</summary>
    public static float Wear(World w, int unitTypeId, float mark) => Value(w, unitTypeId, mark, "wear");

    /// <summary>O nome que se lê de uma marca ("Fuzil modular"), pela geração inteira mais próxima abaixo.
    /// Vazio num mundo sem marcas nenhumas.</summary>
    public static string Name(World w, int unitTypeId, float mark)
    {
        var d = At(w, unitTypeId, mark);
        return d is null ? "" : d.Name;
    }

    /// <summary>A chapa de uma marca.</summary>
    public static string Glyph(World w, int unitTypeId, float mark) => At(w, unitTypeId, mark)?.Glyph ?? "caixa";

    /// <summary>A linha da tabela em que uma marca com casas decimais está pousada (a geração de baixo).</summary>
    public static EquipmentMarkDef? At(World w, int unitTypeId, float mark)
    {
        EquipmentMarkDef? found = null;
        foreach (var m in All(w, unitTypeId))
            if (m.Mark <= mark + 0.0001f || found is null) found = m;
        return found;
    }

    /// <summary>A marca em palavras: "Fuzil modular (Mk III)". É o que a ficha da divisão e a prateleira do
    /// armazém escrevem, e o que a prova headless lê.</summary>
    public static string Describe(World w, int unitTypeId, float mark)
    {
        var d = At(w, unitTypeId, mark);
        return d is null ? "sem marca" : $"{d.Name} ({Roman(d.Mark)})";
    }

    /// <summary>Mk I, Mk II, Mk III… — a convenção com que o material se numera.</summary>
    public static string Roman(int mark)
    {
        string[] n = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return "Mk " + (mark > 0 && mark < n.Length ? n[mark] : mark.ToString());
    }

    /// <summary>Mistura material novo numa pilha que já lá estava: a marca da prateleira é a média pesada
    /// pelas quantidades. Meter dez conjuntos Mk IV num armazém com noventa Mk I não faz um armazém Mk IV.</summary>
    public static float Blend(float haveQty, float haveMark, float addQty, float addMark)
    {
        float total = haveQty + addQty;
        if (total <= 0.0001f) return addMark;
        if (haveQty <= 0.0001f) return addMark;
        return (haveQty * haveMark + addQty * addMark) / total;
    }

    /// <summary>A marca média do material que uma divisão precisa, lida da prateleira do país: é isto que
    /// ela ganha quando se reequipa. Pesa cada tipo pelo que o modelo dele pede.</summary>
    public static float FromWarehouse(World w, Country c, IReadOnlyList<(int UnitTypeId, int Qty)> need)
    {
        float sum = 0f, qty = 0f;
        foreach (var (type, n) in need)
        {
            if (n <= 0) continue;
            sum += n * c.StockedMark(type); qty += n;
        }
        return qty <= 0f ? 0f : sum / qty;
    }

    /// <summary>A marca com que uma divisão sai da fábrica hoje: a melhor que o país tem aberta, pesada
    /// pelo que o modelo pede. Uma divisão nova está sempre na ponta do que a indústria sabe fazer.</summary>
    public static float Fresh(World w, Country c, int templateId)
    {
        var need = w.KitNeed(templateId);
        float sum = 0f, qty = 0f;
        foreach (var (type, n) in need)
        {
            if (n <= 0) continue;
            sum += n * Open(w, c, type); qty += n;
        }
        return qty <= 0f ? 0f : sum / qty;
    }

    /// <summary>Dá marca de origem a uma divisão que não tem nenhuma: as tropas com que se começa o jogo, e
    /// as de um save anterior às marcas, levam ao ombro a primeira geração. Sem tabela de marcas não faz
    /// nada — e o mundo antigo continua a lutar exactamente como lutava.</summary>
    public static void Enlist(World w, Division d)
    {
        if (d.Mark > 0f || w.EquipmentMarks.Count == 0) return;
        var need = w.KitNeed(d.TemplateId);
        float first = 0f, qty = 0f;
        foreach (var (type, n) in need)
        {
            if (n <= 0) continue;
            var list = All(w, type);
            if (list.Count == 0) continue;
            first += n * list[0].Mark; qty += n;
        }
        if (qty > 0f) d.Mark = first / qty;
    }

    /// <summary>O que a marca do material faz à força de uma divisão em combate. Uma divisão sem marca
    /// nenhuma (mundo antigo, save antigo) vale 1 e bate-se exactamente como sempre se bateu.</summary>
    public static float PowerMult(World w, Division d)
    {
        if (d.Mark <= 0f || w.EquipmentMarks.Count == 0) return 1f;
        var need = w.KitNeed(d.TemplateId);
        if (need.Count == 0) return 1f;
        float sum = 0f, qty = 0f;
        foreach (var (type, n) in need)
        {
            if (n <= 0) continue;
            sum += n * Power(w, type, d.Mark); qty += n;
        }
        return qty <= 0f ? 1f : sum / qty;
    }

    /// <summary>O mesmo para o desgaste: material novo estraga-se menos por cada homem que cai.</summary>
    public static float WearMult(World w, Division d)
    {
        if (d.Mark <= 0f || w.EquipmentMarks.Count == 0) return 1f;
        var need = w.KitNeed(d.TemplateId);
        if (need.Count == 0) return 1f;
        float sum = 0f, qty = 0f;
        foreach (var (type, n) in need)
        {
            if (n <= 0) continue;
            sum += n * Wear(w, type, d.Mark); qty += n;
        }
        return qty <= 0f ? 1f : sum / qty;
    }

    /// <summary>A marca que uma linha de produção devia estar a fazer hoje, e o que isso lhe custa em ritmo.
    /// Enquanto a tecnologia não abrir nada de novo devolve a marca que já lá estava e ritmo intacto; quando
    /// abre, a linha para para se reafinar e fica com mark_switch_efficiency do ritmo que tinha.</summary>
    public static (float Mark, float Efficiency) Retool(World w, Country c, int unitTypeId, float mark,
                                                       float efficiency)
    {
        float open = Open(w, c, unitTypeId);
        if (open <= 0f) return (mark, efficiency);
        if (mark <= 0f) return (open, efficiency);                 // linha nova: nasce já na melhor marca
        if (open <= mark + 0.0001f) return (mark, efficiency);
        float step = MathF.Max(0.01f, w.Rule("mark_switch_min", 1f));
        if (open - mark < step) return (mark, efficiency);          // ainda não vale a pena parar a linha
        float keep = Math.Clamp(w.Rule("mark_switch_efficiency", 0.55f), 0f, 1f);
        return (open, MathF.Max(1f, efficiency * keep));
    }
}
