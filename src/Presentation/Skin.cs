using Godot;

namespace WarGame.Presentation;

/// <summary>Quanto do jogo está mesmo vestido com o tema desta casa, e o que ainda vem de fábrica.
///
/// Existe por causa do 0.3.18. O tema era posto em `GetTree().Root.Theme` com um comentário a dizer
/// "tema da janela inteira", e durante muito tempo não era: o Hud é uma CanvasLayer e em Godot o tema de
/// uma janela só desce por Control e por Window, por isso 85 dos 114 botões desenhavam-se com o tema de
/// fábrica. Ninguém deu por isso porque os dois são cinzentos escuros — foi um contador que denunciou.
///
/// A conta é feita contra o tema de fábrica e não contra uma lista escrita à mão: para cada classe de
/// controlo que está na árvore, quantas peças (chapas e cores) o Godot lhe dá e quantas dessas nós
/// substituímos. Uma classe que apareça no jogo sem uma única peça nossa é uma classe que se desenha à
/// maneira do Godot, e é isso que o número de "nuas" conta.
///
/// Não se persegue o 100%: há peças de fábrica que estão bem como estão (contornos de letra a zero,
/// ícones que não usamos). O que interessa é o número mexer quando alguma coisa se desliga.
///
/// Conta os filhos internos também — as barras de scroll de um ScrollContainer são internas, e eram
/// justamente uma das que estavam por vestir.</summary>
public static class Skin
{
    public static string Report(Node root, Theme mine)
    {
        var seen = new SortedDictionary<string, int>();
        Walk(root, seen);
        var factory = ThemeDB.Singleton.GetDefaultTheme();
        int need = 0, got = 0;
        var naked = new List<string>();
        foreach (var (cls, n) in seen)
        {
            int has = 0, ours = 0;
            foreach (var it in factory.GetStyleboxList(cls)) { has++; if (mine.HasStylebox(it, cls)) ours++; }
            foreach (var it in factory.GetColorList(cls)) { has++; if (mine.HasColor(it, cls)) ours++; }
            if (has == 0) continue;                       // caixas e espaçadores não desenham pele nenhuma
            need += has;
            got += ours;
            if (ours == 0) naked.Add($"{cls}×{n}");
        }
        return $"pele: {got} de {need} peças da casa"
             + (naked.Count == 0 ? ", nenhuma classe de fábrica" : $", de fábrica: {string.Join(", ", naked)}");
    }

    private static void Walk(Node root, SortedDictionary<string, int> seen)
    {
        foreach (var child in root.GetChildren(includeInternal: true))
        {
            // Pela variação de tipo quando a há: é por esse nome que o Godot procura a pele do nó, e um
            // controlo com variação não se veste pelo nome da classe.
            if (child is Control c)
            {
                string k = c.ThemeTypeVariation.ToString() is { Length: > 0 } v ? v : c.GetClass();
                seen[k] = seen.GetValueOrDefault(k) + 1;
            }
            Walk(child, seen);
        }
    }
}
