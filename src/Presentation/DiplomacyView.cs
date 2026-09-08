using Godot;
using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>A folha de acções contra um país: tudo o que se lhe pode fazer, numa lista só, com o preço de
/// cada coisa e o motivo de quando não se pode.
///
/// Existe porque a diplomacia estava toda feita e espalhada por três painéis. Justificar guerra só se
/// alcançava pelo painel de uma região dele; o material de guerra e o adido militar viviam no painel da
/// Guerra, em abas próprias; as facções, o mercado e a espionagem na ficha do país. Quem quisesse pensar o
/// que fazer a um vizinho tinha de percorrer os três e adivinhar o que existia em cada um — e não havia
/// sítio nenhum onde se visse a lista das acções possíveis.
///
/// Cada linha pergunta ao próprio comando se pode ser feita — `ICommand.Validate` — e mostra a resposta
/// como está escrita lá. É de propósito: as condições da guerra e dos pactos vivem nos comandos, e uma
/// segunda cópia delas aqui na vista acabava sempre a discordar da primeira. Quando não se pode, a linha
/// diz porquê ("Pacto de não-agressão em vigor") em vez de desaparecer, que um botão que some não ensina
/// nada a ninguém.
///
/// Só lê o World e monta botões: quem valida e muta é o comando, despachado por quem chamou.</summary>
public static class DiplomacyView
{
    /// <summary>Monta a folha. `act` recebe o comando e a pergunta de confirmação (null = fazer já); `found`
    /// abre a caixa de fundar facção; `material` salta para a aba do material no painel da Guerra, que é
    /// onde o empréstimo se mede numa gama e não num sim/não.</summary>
    public static VBoxContainer Sheet(World w, int pid, Country other, Action<ICommand, string?> act,
                                      Action found, Action material)
    {
        // O nome não é enfeite: é por ele que o --smoke encontra a folha e conta as acções. Uma folha que
        // nasça vazia não levanta erro nenhum — some-se, e ninguém dá por ela.
        var v = new VBoxContainer { Name = "Accoes" };
        v.AddThemeConstantOverride("separation", 4);
        var me = w.Countries[pid];

        // Justificar guerra: começa uma contagem que declara a guerra sozinha ao fim dela. Por isso leva
        // pergunta — é a única acção desta folha que não se desfaz.
        float days = w.Rule("war_justify_days", 30f);
        if (me.JustifyTarget == other.Id)
            v.AddChild(Waiting("⚔", "Justificar guerra",
                $"a justificar há {me.JustifyProgress:0} de {days:0} dias — ao fim deles a guerra declara-se sozinha"));
        else
            v.AddChild(Row(w, "⚔", "Justificar guerra", $"{days:0} dias até a guerra rebentar sozinha", "Justificar",
                new JustifyWarCommand(pid, other.Id), act,
                $"Justificar objectivo de guerra contra {other.Name}? Ao fim de {days:0} dias a guerra declara-se sozinha."));

        if (w.AreAtWar(pid, other.Id))
            v.AddChild(Row(w, "🕊", "Propor paz branca", "cada um fica com o que era seu", "Propor",
                new OfferPeaceCommand(pid, other.Id), act, null));
        else
            v.AddChild(Row(w, "📜", "Pacto de não-agressão", $"custa {w.Rule("nap_cost", 20f):0} de política",
                "Propor", new ProposeNonAggressionCommand(pid, other.Id), act, null));

        // Facções: convidar para cada uma das nossas, ou fundar a primeira se não tivermos nenhuma.
        var ours = w.FactionsOf(pid).ToList();
        foreach (var f in ours)
            v.AddChild(Row(w, "🤝", $"Convidar para {f.Name}", $"{f.Members.Count} membros — a aliança chama-os a todos",
                "Convidar", new InviteToFactionCommand(pid, f.Id, other.Id), act, null));
        if (ours.Count == 0)
            v.AddChild(Go("＋", "Fundar facção", "sem facção não há a quem convidar ninguém", "Fundar", found));

        v.AddChild(Row(w, "🎖", "Adido militar", $"{w.Rule("attache_cost_per_day", 0.5f):0.0}/dia — vê a guerra dele por dentro",
            "Enviar", new SendAttacheCommand(pid, other.Id), act, null));

        // Voluntários: a única ajuda que custa sangue. Quando já lá temos gente, a linha deixa de oferecer
        // mais e passa a oferecer o regresso — que é a pergunta que o jogador faz a seguir.
        int away = VolunteerSystem.Away(w, pid), cap = VolunteerSystem.Cap(w, pid);
        int here = w.Divisions.Values.Count(d => d.VolunteerFrom == pid && d.CountryId == other.Id);
        if (here > 0)
            v.AddChild(Row(w, "🤝", "Chamar os voluntários",
                $"{here} {(here == 1 ? "divisão nossa bate-se" : "divisões nossas batem-se")} por {other.Name}",
                "Chamar", new RecallVolunteersCommand(pid, other.Id), act,
                $"Chamar de volta as nossas {here} divisões que se batem por {other.Name}? Voltam à capital hoje mesmo."));
        else
            v.AddChild(Row(w, "🤝", "Voluntários", $"{away} de {cap} lá fora — batem-se por eles, custam-nos a nós",
                "Mandar", new SendVolunteersCommand(pid, other.Id), act,
                $"Mandar uma divisão nossa para a guerra de {other.Name}? Passa a combater sob a bandeira dele, mas os homens e o material continuam a sair do nosso."));

        v.AddChild(Go("📦", "Material de guerra", "uma fatia do nosso rendimento, todos os dias", "Abrir", material));
        return v;
    }

    /// <summary>Linha com comando: o botão só liga se o comando disser que sim, e quando diz que não é a
    /// frase dele que fica na linha.</summary>
    private static Control Row(World w, string icon, string name, string hint, string verb, ICommand cmd,
                               Action<ICommand, string?> act, string? ask)
    {
        string? why = cmd.Validate(w);
        var b = Ui.Btn(verb, () => act(cmd, ask), 150, why is null ? Ui.Kind.Primary : Ui.Kind.Normal);
        b.Disabled = why is not null;
        if (why is not null) b.TooltipText = why;
        return Plate(icon, name, why ?? hint, why is not null, b);
    }

    /// <summary>Linha que leva a outro sítio: não há comando para validar, o painel de destino é que sabe.</summary>
    private static Control Go(string icon, string name, string hint, string verb, Action go)
        => Plate(icon, name, hint, false, Ui.Btn(verb, go, 150));

    /// <summary>Linha sem botão: já está a acontecer e só falta esperar.</summary>
    private static Control Waiting(string icon, string name, string hint)
        => Plate(icon, name, hint, false, Ui.Lbl("em curso", 15));

    private static Control Plate(string icon, string name, string hint, bool barred, Control tail)
    {
        var plate = new PanelContainer();
        plate.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Surface.Darkened(0.15f), 6));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10); plate.AddChild(row);

        var ic = Ui.Lbl(icon, 20);
        ic.AddThemeColorOverride("font_color", barred ? Ui.TextDim : Ui.Accent);
        ic.CustomMinimumSize = new Vector2(28, 0);
        row.AddChild(ic);

        var stack = new VBoxContainer(); stack.AddThemeConstantOverride("separation", 0);
        var lbl = Ui.Lbl(name, 18);
        lbl.AddThemeColorOverride("font_color", barred ? Ui.TextDim : Ui.Text);
        stack.AddChild(lbl);
        var note = Ui.Lbl(hint, 13);
        note.AddThemeColorOverride("font_color", barred ? Ui.Danger : Ui.TextDim);
        stack.AddChild(note);
        row.AddChild(Ui.Grow(stack));

        tail.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(tail);
        return plate;
    }
}
