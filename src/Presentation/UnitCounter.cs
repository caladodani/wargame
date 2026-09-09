using Godot;

namespace WarGame.Presentation;

/// <summary>Contador de unidades como os dos mapas de estado-maior — e como os do HoI4: uma caixa com a
/// faixa da bandeira em cima, o símbolo NATO do tipo de tropa à esquerda (✕ infantaria, oval blindado, oval
/// com ✕ mecanizada, ponto apoio, ✕ com asas pára-quedistas), o número de divisões à direita, as barras de
/// organização e de resistência coladas ao fundo, e os dentes da trincheira por baixo.
///
/// Existe porque a pastilha com um número dizia quantas divisões havia e mais nada: para saber se aquela
/// pilha estava fresca, partida ou entrincheirada era preciso tocar na região e ler o painel. Ao aproximar o
/// mapa, o contador responde a isso sem um toque — que é exactamente o que faz o mapa do HoI4 parecer um
/// mapa de guerra e não uma manta de cores.
///
/// Só desenha: quem o enche é o RegionRenderer, e a caixa fica por baixo do centro da região para não tapar
/// a pastilha dos marcos (capital, forte, porto, objectivo).</summary>
public partial class UnitCounter : Node2D
{
    public const float BoxW = 104f, BoxH = 56f, DropY = 20f;
    /// <summary>Largura da coluna dos galões de veterania, no canto de dentro da caixa.</summary>
    public const float ChevronW = 10f;

    /// <summary>Largura da chapa do estado, pendurada do lado de fora da caixa.</summary>
    public const float TagW = 22f;

    private Color _tint = Colors.Gray;
    private string _kind = "infantry", _spec = "", _state = "";
    private int _count, _chevrons;
    private float _org = 1f, _hp = 1f, _entrench, _kit = 1f;
    private bool _known, _picked;
    private Texture2D? _flag;
    private Label _num = null!;

    /// <summary>Esta pilha está marcada pelo jogador: a caixa levanta-se e ganha os cantos brancos, que é
    /// como o HoI4 diz qual é a unidade que tem em mãos. Mexer-lhe repinta só esta caixa.</summary>
    public bool Picked
    {
        get => _picked;
        set { if (_picked != value) { _picked = value; QueueRedraw(); } }
    }

    /// <summary>--smoke: a chapa do estado que esta caixa está a desenhar ("" = tropa alheia, sem estado).</summary>
    public string State => _state;

    public override void _Ready()
    {
        _num = new Label
        {
            Name = "Count",
            Position = new Vector2(6f, DropY + 6f),
            Size = new Vector2(BoxW / 2f - 12f, BoxH - 22f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { FontSize = 28, FontColor = Colors.White, OutlineSize = 6, OutlineColor = new Color(0, 0, 0, 0.9f) },
        };
        AddChild(_num);
    }

    /// <summary>Enche o contador. `known` distingue a nossa tropa (barras verdadeiras) da tropa alheia, de
    /// que só se sabe o que se vê de fora: quantas divisões e de que tipo. `chevrons` são os galões do grau
    /// de veterania da pilha (Veterancy) — que também são coisa de dentro de casa: da tropa alheia não se
    /// sabe se é gente verde se é elite enquanto não se lhe bater. `kit` é o material que a pilha tem (1 =
    /// armada até aos dentes): entra na barra da resistência como um tecto marcado a giz, porque é isso que
    /// ele é — acima daquele risco a tropa não recupera enquanto não lhe chegar equipamento do armazém.</summary>
    public void Set(Color tint, Texture2D? flag, string kind, int count, float org, float hp, float entrench, bool known,
                    string spec = "", int chevrons = 0, float kit = 1f, string state = "")
    {
        _tint = tint; _flag = flag; _kind = kind; _spec = spec; _count = count; _chevrons = Mathf.Clamp(chevrons, 0, 4);
        _org = org; _hp = hp; _entrench = entrench; _known = known; _kit = Mathf.Clamp(kit, 0f, 1f);
        _state = Glyph.Knows(state) ? state : "";
        if (_num is not null)
        {
            _num.Text = count.ToString();
            // com galões à esquerda, o número afasta-se deles para não ficarem colados
            _num.Position = new Vector2(6f + (_chevrons > 0 ? ChevronW + 2f : 0f), DropY + 6f);
            _num.Size = new Vector2(BoxW / 2f - 12f - (_chevrons > 0 ? ChevronW + 2f : 0f), BoxH - 22f);
        }
        QueueRedraw();
    }

    /// <summary>Tipo de tropa a partir das etiquetas do modelo (unit_tag): o símbolo diz o que ali está sem
    /// se ler nada, que é para isso que os contadores existem.</summary>
    public static string KindOf(IEnumerable<string> tags)
    {
        var t = new HashSet<string>(tags);
        if (t.Contains("airborne")) return "airborne";
        if (t.Contains("armored")) return t.Contains("infantry") ? "mech" : "armor";
        if (t.Contains("infantry")) return "infantry";
        return "support";
    }

    /// <summary>--smoke: os galões que esta caixa está a desenhar.</summary>
    public int Chevrons => _chevrons;

    /// <summary>--smoke: o material da pilha desta caixa (1 = sem falta, sem caixote desenhado).</summary>
    public float Kit => _kit;

    /// <summary>A caixa em coordenadas locais do contador. Serve o desenho e serve o toque: é a mesma
    /// figura que o RegionRenderer usa para saber se o dedo caiu em cima desta pilha.</summary>
    public static Rect2 Box() => new(-BoxW / 2f, DropY, BoxW, BoxH);

    public override void _Draw()
    {
        var box = Box();

        // Marcado: a caixa levanta-se do mapa. Um halo por trás e os quatro cantos brancos por fora, que é a
        // leitura do HoI4 — a unidade que se tem em mãos não se confunde com as outras dez que estão à volta.
        if (_picked)
        {
            var halo = box.Grow(7f);
            DrawRect(halo, new Color(1f, 0.95f, 0.7f, 0.16f));
            DrawRect(halo, new Color(1f, 0.95f, 0.7f, 0.55f), false, 2f);
            float arm = 16f;
            foreach (var (corner, dx, dy) in new[]
                     { (halo.Position, 1f, 1f), (new Vector2(halo.End.X, halo.Position.Y), -1f, 1f),
                       (halo.End, -1f, -1f), (new Vector2(halo.Position.X, halo.End.Y), 1f, -1f) })
            {
                DrawLine(corner, corner + new Vector2(arm * dx, 0f), Colors.White, 3f);
                DrawLine(corner, corner + new Vector2(0f, arm * dy), Colors.White, 3f);
            }
        }

        DrawRect(box, _tint.Darkened(0.78f) with { A = 0.93f });

        // faixa da bandeira colada ao topo da caixa (ou uma barra da cor do país quando não há bandeira)
        var band = new Rect2(box.Position.X + 2f, box.Position.Y + 2f, box.Size.X - 4f, 10f);
        if (_flag is not null) DrawTextureRect(_flag, band, false);
        else DrawRect(band, _tint);

        DrawRect(box, _tint.Lightened(0.15f), false, 3f);
        // rebites nos cantos: a moldura de metal dos painéis do HoI4, à escala do mapa
        var rivet = _tint.Lightened(0.55f) with { A = 0.85f };
        foreach (var corner in new[] { box.Position + new Vector2(6f, 18f), box.Position + new Vector2(box.Size.X - 6f, 18f),
                                       box.End + new Vector2(-6f, -6f), new Vector2(box.Position.X + 6f, box.End.Y - 6f) })
            DrawCircle(corner, 2.2f, rivet);

        NatoSymbol.Draw(this, new Rect2(box.Position.X + BoxW / 2f + 6f, box.Position.Y + 16f, BoxW / 2f - 14f, BoxH - 30f), _kind, 2.6f, _spec);

        // galões da veterania, empilhados no canto de dentro: um risco para tropa treinada, três para elite.
        // É a mesma leitura que o HoI4 dá ao contador — de longe vê-se se aquela pilha é gente verde ou gente
        // que já fez a campanha toda, sem se abrir ficha nenhuma.
        var gold = new Color(1f, 0.82f, 0.25f);
        for (int i = 0; i < _chevrons; i++)
        {
            float cx = box.Position.X + 5f, cy = box.Position.Y + 18f + i * 7f;
            DrawPolyline(new[] { new Vector2(cx, cy + 4f), new Vector2(cx + ChevronW / 2f, cy), new Vector2(cx + ChevronW, cy + 4f) },
                         new Color(0, 0, 0, 0.8f), 3.4f);
            DrawPolyline(new[] { new Vector2(cx, cy + 4f), new Vector2(cx + ChevronW / 2f, cy), new Vector2(cx + ChevronW, cy + 4f) },
                         gold, 1.8f);
        }

        // barras: organização por cima da resistência, em pé de igualdade com as do painel da região
        float y = box.End.Y - 12f, w = box.Size.X - 12f, x = box.Position.X + 6f;
        DrawRect(new Rect2(x, y, w, 4f), new Color(0, 0, 0, 0.55f));
        DrawRect(new Rect2(x, y + 6f, w, 4f), new Color(0, 0, 0, 0.55f));
        var orgColor = _known ? new Color(0.45f, 0.78f, 0.98f) : new Color(0.55f, 0.55f, 0.6f);
        var hpColor = _known ? (_hp > 0.5f ? new Color(0.45f, 0.85f, 0.5f) : new Color(0.95f, 0.5f, 0.35f))
                             : new Color(0.45f, 0.45f, 0.5f);
        DrawRect(new Rect2(x, y, w * Mathf.Clamp(_known ? _org : 1f, 0f, 1f), 4f), orgColor);
        DrawRect(new Rect2(x, y + 6f, w * Mathf.Clamp(_known ? _hp : 1f, 0f, 1f), 4f), hpColor);

        // tecto do material: a parte da barra da resistência que a falta de equipamento fecha fica riscada,
        // com um risco branco no sítio onde ela pára de subir. É a leitura que o HoI4 dá com o triângulo do
        // equipamento em falta — aqui vive na própria barra, que é onde interessa: aquela pilha não passa dali.
        if (_known && _kit < 0.995f)
        {
            float roof = x + w * _kit;
            var chalk = new Color(0.95f, 0.78f, 0.3f, 0.9f);
            for (float hx = roof + 2f; hx < x + w; hx += 4f)
                DrawLine(new Vector2(hx, y + 6f), new Vector2(hx - 3f, y + 10f), chalk with { A = 0.55f }, 1f);
            DrawLine(new Vector2(roof, y + 5f), new Vector2(roof, y + 11f), Colors.White, 1.6f);

            // caixote por armar, debaixo da caixa e do lado de fora dos dentes da trincheira: âmbar quando
            // falta pouco, vermelho quando a pilha anda a meio gás
            var crate = new Rect2(box.End.X - 16f, box.End.Y + 2f, 12f, 8f);
            var ink = _kit < 0.6f ? new Color(0.95f, 0.35f, 0.3f) : chalk;
            DrawRect(crate, new Color(0, 0, 0, 0.55f));
            DrawRect(crate, ink, false, 1.4f);
            DrawLine(crate.Position, crate.End, ink, 1.2f);
            DrawLine(new Vector2(crate.End.X, crate.Position.Y), new Vector2(crate.Position.X, crate.End.Y), ink, 1.2f);
        }

        // chapa do estado, pendurada do lado de fora da caixa: o que aquela pilha está a fazer agora — a
        // marchar, a bater-se, cercada, a ir de comboio. É a leitura que no HoI4 vem da seta e dos ícones
        // colados ao contador, e é a razão por que ali não é preciso abrir a ficha da província para saber
        // se a caixa está parada. Da tropa alheia não se desenha: isso é coisa que se sabe de dentro.
        if (_state.Length > 0)
        {
            var tag = new Rect2(box.Position.X - TagW - 3f, box.Position.Y + (BoxH - TagW) / 2f, TagW, TagW);
            DrawRect(tag, new Color(0.05f, 0.05f, 0.06f, 0.9f));
            DrawRect(tag, _tint.Lightened(0.35f), false, 1.6f);
            Glyph.Draw(this, tag.Grow(-3f), _state, new Color(0.96f, 0.93f, 0.85f), 1.7f);
        }

        // dentes da trincheira, um por degrau cavado: a frente parada vê-se de longe
        int teeth = Mathf.Clamp(Mathf.FloorToInt(_entrench), 0, 8);
        var sand = new Color(0.85f, 0.75f, 0.45f, 0.95f);
        for (int i = 0; i < teeth; i++)
        {
            float tx = box.Position.X + 6f + i * 8f;
            DrawRect(new Rect2(tx, box.End.Y + 3f, 5f, 5f), sand);
        }
    }

}
