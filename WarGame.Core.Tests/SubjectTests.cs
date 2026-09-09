using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Estados-fantoche: ganhar a guerra sem comer o mapa. O que se prova aqui é que os degraus vêm da
/// tabela e não do código, que o degrau se lê da autonomia (e por isso nunca a pode contradizer), que o
/// tributo é uma fatia do dia que o vassalo acabou de ganhar — contada pelas mesmas funções do rendimento e
/// dos homens — e que a coleira se solta sozinha, mais depressa a quem se bate.</summary>
public class SubjectTests
{
    private static (World w, MsSqliteDatabase db) Vassalage()
    {
        var (w, db) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Register(new EconomySystem());     // o rendimento do dia
        w.Register(new ManpowerSystem());    // e os homens do dia
        w.Register(new SubjectSystem());     // é sobre eles que se cobra
        return (w, db);
    }




    /// <summary>O tributo não é uma segunda conta do rendimento: é a fatia do degrau sobre a mesma função
    /// pública com que o EconomySystem paga o dia. Se alguém mudar a fórmula do rendimento, o tributo muda
    /// com ela — que é a razão de ele não ter cópia nenhuma.</summary>
    [Fact]
    public void TheTributeIsASliceOfTheSameDayTheVassalEarned()
    {
        var (w, db) = Vassalage(); using var _ = db;
        Subjects.Puppet(w, 1, 2);
        var sub = w.Countries[2];
        var lvl = Subjects.Level(w, sub)!;

        Assert.Equal(EconomySystem.Income(w, 2) * lvl.YieldShare, Subjects.Tribute(w, sub), 4);
        Assert.Equal(ManpowerSystem.Gain(w, sub, ManpowerSystem.Pop(w, 2)) * lvl.ManpowerShare, Subjects.Levy(w, sub), 4);
    }










}
