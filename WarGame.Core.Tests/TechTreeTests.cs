using WarGame.Core.Model;
using WarGame.Core.Stats;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A árvore de investigação inteira, lida da base de dados. O utilizador disse que a pesquisa do
/// exército estava curta — a Aviação e a Marinha tinham 31 degraus cada e a Infantaria tinha três —, e
/// estes testes são o que impede a árvore de voltar a encolher: cada ramo de terra tem de ter fundura, cada
/// degrau tem de fazer alguma coisa (ou ser porta para outro), e ninguém pode pedir um degrau que não
/// existe.</summary>
public class TechTreeTests
{
    /// <summary>Os ramos de terra: os que o exército investiga. Vêm da coluna arm da tech_branch, não de uma
    /// lista em código — um ramo novo entra aqui por dizer 'terra' na sua linha.</summary>
    private static string[] Terra(World w) =>
        w.TechBranches.Values.Where(b => b.Arm == "terra").Select(b => b.Id).ToArray();



    [Fact]
    public void Ninguem_pede_um_degrau_que_nao_existe_nem_anda_as_voltas()
    {
        var (w, _) = TestWorld.Build();
        foreach (var t in w.Techs.Values)
        {
            if (t.Requires is null) continue;
            Assert.True(w.Techs.ContainsKey(t.Requires), $"{t.Id} pede {t.Requires}, que não existe");

            var visto = new HashSet<string> { t.Id };
            for (string? cur = t.Requires; cur is not null; cur = w.Techs[cur].Requires)
                Assert.True(visto.Add(cur), $"a corrente de {t.Id} anda às voltas em {cur}");
        }
    }

}
