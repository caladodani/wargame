using WarGame.Core.Commands;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>A ORDEM ANTES DE SER DADA: o que acontece se a tropa desta província for mandada para aquela.
///
/// Nasceu com o arrasto do contador (o gesto do HoI4: agarrar a pilha e puxar até ao destino). Uma seta que
/// se desenha atrás do dedo tem de dizer, antes de o dedo se levantar, três coisas — quantas divisões vão,
/// quantos dias leva, e se aquilo sequer se pode fazer. Sem isto o jogador larga a ordem às cegas e só
/// descobre pelo aviso de recusa, que é a pior altura para descobrir.
///
/// A legalidade não é decidida aqui: quem decide é o próprio <see cref="MoveDivisionCommand"/>, e é por isso
/// que a seta nunca pode prometer uma ordem que o núcleo depois recusa. Os dias saem do mesmo cálculo que faz
/// o mundo andar (<see cref="MovementSystem.HopDays"/>), salto a salto pelo caminho que a ordem vai usar.
///
/// O nome e a chapa da ordem vêm da tabela `stack_state` — marcha para terra livre, combate para terra de
/// quem combatemos. É a mesma tabela que o contador usa para dizer o que a pilha está a fazer, e por isso a
/// seta e a caixa falam a mesma língua.
///
/// Estado derivado: não guarda nada, não é ISystem, não entra no save.</summary>
public static class Orders
{
    /// <summary>O que sai de arrastar esta pilha para ali: quantas divisões partem, por que caminho, em
    /// quantos dias, e o que a impede quando não pode ser. `State` é a linha de `stack_state` que descreve a
    /// ordem (marcha ou combate) — vazia quando não há ordem nenhuma para dar.</summary>
    public readonly record struct OrderPreview(int Divisions, int Hops, float Days, bool Legal, string Why,
                                               string State, string Name, string Glyph, bool Hostile);

    /// <summary>Olhar para a ordem sem a dar. Nada aqui muta o mundo.</summary>
    public static OrderPreview Look(World w, int countryId, int fromRegionId, int toRegionId)
    {
        string state = "", name = "", glyph = "";
        bool hostile = w.Regions.TryGetValue(toRegionId, out var target) && w.IsHostile(countryId, target);

        var mine = w.Regions.TryGetValue(fromRegionId, out var from)
            ? from.DivisionIds.Select(id => w.Divisions.GetValueOrDefault(id)).OfType<Division>()
                  .Where(d => d.CountryId == countryId).ToList()
            : new List<Division>();
        if (mine.Count == 0) return new OrderPreview(0, 0, 0f, false, "Não há tropa tua nessa província", state, name, glyph, hostile);
        if (target is null) return new OrderPreview(mine.Count, 0, 0f, false, "Região inexistente", state, name, glyph, hostile);

        // a que manda é a primeira pela ordem do id: se a ordem for boa para ela, a seta pode prometê-la
        var lead = mine.OrderBy(d => d.Id).First();
        string? err = new MoveDivisionCommand(countryId, lead.Id, toRegionId).Validate(w);
        var path = MoveDivisionCommand.FindPath(w, lead.RegionId, toRegionId, countryId);

        float days = 0f;
        if (path is not null)
        {
            int at = lead.RegionId;
            foreach (int hop in path)
            {
                if (!w.Regions.TryGetValue(at, out var a) || !w.Regions.TryGetValue(hop, out var b)) break;
                days += MovementSystem.HopDays(w, lead, a, b);
                at = hop;
            }
        }

        if (err is null)
        {
            state = hostile ? "combate" : "marcha";
            var def = w.StackStates.GetValueOrDefault(state);
            name = def?.Name ?? ""; glyph = def?.Glyph ?? "";
        }
        return new OrderPreview(mine.Count, path?.Count ?? 0, days, err is null, err ?? "", state, name, glyph, hostile);
    }

    /// <summary>A linha que a seta escreve por cima do mapa enquanto o dedo anda.</summary>
    public static string Line(World w, OrderPreview p, string destName) =>
        !p.Legal ? (p.Why.Length > 0 ? p.Why : "não dá") :
        $"{(p.Name.Length > 0 ? p.Name : "Em marcha")} para {destName} · {p.Days:0.#} d · "
        + $"{p.Divisions} divis{(p.Divisions == 1 ? "ão" : "ões")}"
        + (p.Hops > 1 ? $" ({p.Hops} saltos)" : "");
}
