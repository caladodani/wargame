using Godot;
using WarGame.Core.Model;

namespace WarGame.Presentation;

/// <summary>Chapas desenhadas à mão, no lugar dos emoji.
///
/// O jogo mostrava um emoji em todo o sítio onde o HoI4 mostra uma chapa gravada: 🏭 na fábrica, 🔬 no
/// laboratório, 🪖 no ramo da infantaria. Um emoji é a única coisa no ecrã que não é nossa — vem do
/// telemóvel, muda de aparência de aparelho para aparelho, é redondo e colorido no meio de um jogo de
/// tinta e metal, e no S24 sai desenhado pela Noto Color Emoji com um brilho de autocolante. O
/// UnitSymbol já provava que se desenha melhor à mão: os símbolos NATO do mapa são linhas nossas.
///
/// Estas chapas são silhuetas, ao traço, na cor que a moldura pedir. Nenhuma delas é arte da Paradox nem
/// copiada de lado nenhum: são as formas que qualquer manual desenha — um capacete, uma lagarta, um obus,
/// uma asa, uma âncora, um telhado de fábrica, um frasco, uma bigorna, um livro, um camião, um átomo, um
/// floco de neve, um sol, um globo, um punho erguido.
///
/// Qual é a chapa de quê não está aqui: vem da coluna `glyph` das tabelas que a têm — building,
/// tech_branch, season, map_mode, air_mission, naval_mission, chronicle_kind, cabinet_slot,
/// occupation_policy e wound_kind. Este ficheiro só sabe desenhar; a base de dados é que sabe o que se
/// desenha onde. O que não conhecer sai como roda dentada — a peça neutra, que é melhor do que um buraco.
///
/// A coluna `icon` (o emoji) fica onde está: é o que se lê no texto corrido de um aviso ou de uma linha da
/// crónica, onde não há nó nenhum para desenhar. A chapa serve as fichas, os botões e as listas — os
/// sítios em que o emoji se via como emoji.</summary>
public static partial class Glyph        // partial: leva um nó Godot lá dentro (GD0002)
{
    /// <summary>Todos os nomes que este ficheiro sabe desenhar. É público para o contador do smoke poder
    /// perguntar quantos dos que a base de dados pede é que existem mesmo.</summary>
    public static readonly string[] Known =
    {
        "capacete", "lagarta", "obus", "asa", "ancora", "drone", "camiao",
        "fabrica", "livro", "frasco", "atomo", "bigorna", "estrada", "escudo",
        "floco", "chuva", "sol", "folha", "globo", "caixa", "punho", "gente",
        "bomba", "alvo", "luneta", "roda",
        // a segunda leva: crónica, gabinete, ocupação e baixas no comando
        "espadas", "pomba", "bandeira", "medalha", "coluna", "coroa", "aperto", "fita",
        "galao", "taca", "barco", "estilhaco", "pasta", "corrente", "balanca", "caveira",
        "megafone", "penso", "gota", "cruz", "paraquedas", "onda",
        // a barra de cima: as duas que não vêm de tabela nenhuma — o cofre e o barril
        "cofre", "barril",
        // o chão: um por terreno, para a região se ver antes de se ler
        "campo", "arvore", "cidade", "montanha", "duna", "gelo",
    };

    public static bool Knows(string name) => System.Array.IndexOf(Known, name) >= 0;

    /// <summary>Desenha a chapa dentro do rectângulo, na cor dada. `r` é o espaço todo: o desenho encolhe
    /// para dentro dele, porque uma chapa colada à borda de um botão lê-se mal.</summary>
    public static void Draw(CanvasItem ci, Rect2 r, string name, Color ink, float thick = 1.8f)
    {
        // caixa útil quadrada e centrada: a chapa nunca se deforma com a moldura
        float side = Mathf.Min(r.Size.X, r.Size.Y) * 0.86f;
        var box = new Rect2(r.Position + (r.Size - new Vector2(side, side)) / 2f, side, side);
        float u = side;                                   // unidade: tudo abaixo é fracção do lado
        Vector2 P(float x, float y) => box.Position + new Vector2(x * u, y * u);
        void Line(float x1, float y1, float x2, float y2, float w = 1f) => ci.DrawLine(P(x1, y1), P(x2, y2), ink, thick * w);
        void Poly(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = P(xy[i * 2], xy[i * 2 + 1]);
            ci.DrawPolyline(pts, ink, thick);
        }
        void Fill(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = P(xy[i * 2], xy[i * 2 + 1]);
            ci.DrawColoredPolygon(pts, ink);
        }
        void Ring(float cx, float cy, float rad, float w = 1f) => ci.DrawArc(P(cx, cy), rad * u, 0f, Mathf.Tau, 24, ink, thick * w);
        void Arc(float cx, float cy, float rad, float a0, float a1, float w = 1f)
            => ci.DrawArc(P(cx, cy), rad * u, a0, a1, 16, ink, thick * w);
        void Dot(float cx, float cy, float rad) => ci.DrawCircle(P(cx, cy), rad * u, ink);

        switch (name)
        {
            // Capacete de aço visto de lado: a calote e a aba, que é o que o faz ler como capacete.
            case "capacete":
                Arc(0.5f, 0.60f, 0.30f, Mathf.Pi, Mathf.Tau, 1.15f);
                Line(0.14f, 0.60f, 0.86f, 0.60f, 1.15f);
                Line(0.10f, 0.66f, 0.90f, 0.66f);
                Line(0.10f, 0.66f, 0.14f, 0.60f);
                Line(0.90f, 0.66f, 0.86f, 0.60f);
                break;

            // Carro de combate de perfil: casco, torre, cano e as rodas da lagarta.
            case "lagarta":
                Poly(0.10f, 0.56f, 0.90f, 0.56f, 0.90f, 0.42f, 0.66f, 0.42f, 0.60f, 0.30f, 0.36f, 0.30f, 0.32f, 0.42f, 0.10f, 0.42f, 0.10f, 0.56f);
                Line(0.60f, 0.36f, 0.94f, 0.36f, 1.1f);       // cano
                Arc(0.30f, 0.66f, 0.10f, 0f, Mathf.Pi);
                Arc(0.70f, 0.66f, 0.10f, 0f, Mathf.Pi);
                Line(0.20f, 0.66f, 0.80f, 0.66f);
                foreach (float x in new[] { 0.36f, 0.50f, 0.64f }) Dot(x, 0.66f, 0.045f);
                break;

            // Obus: a granada a subir em diagonal com o rasto da carga.
            case "obus":
                Poly(0.34f, 0.72f, 0.34f, 0.44f, 0.50f, 0.20f, 0.66f, 0.44f, 0.66f, 0.72f, 0.34f, 0.72f);
                Line(0.34f, 0.56f, 0.66f, 0.56f);
                Line(0.40f, 0.80f, 0.34f, 0.94f);
                Line(0.50f, 0.80f, 0.50f, 0.96f);
                Line(0.60f, 0.80f, 0.66f, 0.94f);
                break;

            // Asa em delta vista de cima: fuselagem, asas para trás e os estabilizadores.
            case "asa":
                Fill(0.50f, 0.10f, 0.60f, 0.44f, 0.94f, 0.66f, 0.94f, 0.74f, 0.56f, 0.66f, 0.54f, 0.84f, 0.68f, 0.92f, 0.68f, 0.96f,
                     0.32f, 0.96f, 0.32f, 0.92f, 0.46f, 0.84f, 0.44f, 0.66f, 0.06f, 0.74f, 0.06f, 0.66f, 0.40f, 0.44f);
                break;

            // Âncora: haste, cepo e as unhas.
            case "ancora":
                Ring(0.50f, 0.18f, 0.09f);
                Line(0.50f, 0.27f, 0.50f, 0.84f, 1.15f);
                Line(0.26f, 0.38f, 0.74f, 0.38f, 1.15f);
                Arc(0.50f, 0.56f, 0.30f, Mathf.Pi * 0.18f, Mathf.Pi * 0.82f, 1.15f);
                Line(0.20f, 0.60f, 0.13f, 0.52f);
                Line(0.80f, 0.60f, 0.87f, 0.52f);
                break;

            // Drone de quatro braços visto de cima: cruz, quatro anéis de hélice, corpo no meio.
            case "drone":
                Line(0.22f, 0.22f, 0.78f, 0.78f);
                Line(0.78f, 0.22f, 0.22f, 0.78f);
                foreach (var (x, y) in new[] { (0.20f, 0.20f), (0.80f, 0.20f), (0.20f, 0.80f), (0.80f, 0.80f) })
                    Ring(x, y, 0.13f);
                Fill(0.40f, 0.42f, 0.60f, 0.42f, 0.60f, 0.58f, 0.40f, 0.58f);
                break;

            // Camião de perfil: caixa, cabina e as duas rodas.
            case "camiao":
                Poly(0.06f, 0.34f, 0.54f, 0.34f, 0.54f, 0.66f, 0.06f, 0.66f, 0.06f, 0.34f);
                Poly(0.54f, 0.46f, 0.72f, 0.46f, 0.84f, 0.58f, 0.94f, 0.58f, 0.94f, 0.66f, 0.54f, 0.66f);
                Dot(0.24f, 0.72f, 0.085f);
                Dot(0.78f, 0.72f, 0.085f);
                break;

            // Fábrica: o telhado de dentes de serra e a chaminé a fumar, que é como se desenha desde sempre.
            case "fabrica":
                Poly(0.06f, 0.84f, 0.06f, 0.52f, 0.28f, 0.66f, 0.28f, 0.52f, 0.50f, 0.66f, 0.50f, 0.52f, 0.72f, 0.66f, 0.72f, 0.84f, 0.06f, 0.84f);
                Poly(0.78f, 0.84f, 0.78f, 0.24f, 0.90f, 0.24f, 0.90f, 0.84f);
                Arc(0.84f, 0.16f, 0.07f, Mathf.Pi, Mathf.Tau, 0.8f);
                break;

            // Livro aberto: as duas folhas e a lombada.
            case "livro":
                Poly(0.50f, 0.30f, 0.12f, 0.24f, 0.12f, 0.74f, 0.50f, 0.80f, 0.88f, 0.74f, 0.88f, 0.24f, 0.50f, 0.30f);
                Line(0.50f, 0.30f, 0.50f, 0.80f, 1.15f);
                Line(0.20f, 0.40f, 0.42f, 0.44f, 0.7f);
                Line(0.58f, 0.44f, 0.80f, 0.40f, 0.7f);
                Line(0.20f, 0.54f, 0.42f, 0.58f, 0.7f);
                Line(0.58f, 0.58f, 0.80f, 0.54f, 0.7f);
                break;

            // Frasco de laboratório: gargalo, corpo cónico e o líquido lá dentro.
            case "frasco":
                Line(0.38f, 0.14f, 0.62f, 0.14f, 1.15f);
                Poly(0.42f, 0.14f, 0.42f, 0.40f, 0.20f, 0.82f, 0.80f, 0.82f, 0.58f, 0.40f, 0.58f, 0.14f);
                Fill(0.29f, 0.62f, 0.71f, 0.62f, 0.80f, 0.82f, 0.20f, 0.82f);
                break;

            // Átomo: núcleo e as três órbitas cruzadas.
            case "atomo":
                Dot(0.50f, 0.50f, 0.08f);
                for (int i = 0; i < 3; i++)
                {
                    float a = Mathf.Pi * i / 3f;
                    var c = P(0.50f, 0.50f);
                    var pts = new Vector2[33];
                    for (int k = 0; k < pts.Length; k++)
                    {
                        float t = Mathf.Tau * k / (pts.Length - 1);
                        var e = new Vector2(Mathf.Cos(t) * 0.42f * u, Mathf.Sin(t) * 0.17f * u);
                        pts[k] = c + e.Rotated(a);
                    }
                    ci.DrawPolyline(pts, ink, thick * 0.85f);
                }
                break;

            // Bigorna: a mesa, o corno e o pé — a chapa do arsenal.
            case "bigorna":
                Fill(0.10f, 0.34f, 0.78f, 0.34f, 0.92f, 0.42f, 0.72f, 0.46f, 0.62f, 0.46f, 0.58f, 0.62f,
                     0.72f, 0.78f, 0.72f, 0.84f, 0.28f, 0.84f, 0.28f, 0.78f, 0.42f, 0.62f, 0.38f, 0.46f, 0.10f, 0.46f);
                break;

            // Estrada a fugir para o horizonte: as duas bermas a fechar e o tracejado do meio.
            case "estrada":
                Poly(0.06f, 0.90f, 0.38f, 0.14f, 0.62f, 0.14f, 0.94f, 0.90f);
                Line(0.50f, 0.20f, 0.50f, 0.34f, 0.8f);
                Line(0.50f, 0.44f, 0.50f, 0.62f, 0.8f);
                Line(0.50f, 0.72f, 0.50f, 0.94f, 0.8f);
                break;

            // Escudo com a barra do reforço: a chapa da fortificação.
            case "escudo":
                Poly(0.50f, 0.10f, 0.88f, 0.24f, 0.88f, 0.54f, 0.50f, 0.90f, 0.12f, 0.54f, 0.12f, 0.24f, 0.50f, 0.10f);
                Line(0.24f, 0.42f, 0.76f, 0.42f, 0.85f);
                break;

            // Floco de neve: os três eixos e os garfos das pontas — o Inverno.
            case "floco":
                for (int i = 0; i < 3; i++)
                {
                    float a = Mathf.Pi * i / 3f;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.38f * u;
                    var c = P(0.5f, 0.5f);
                    ci.DrawLine(c - d, c + d, ink, thick);
                    foreach (float s in new[] { -1f, 1f })
                        foreach (float g in new[] { -0.5f, 0.5f })
                            ci.DrawLine(c + d * s, c + d * s * 0.66f + d.Rotated(Mathf.Pi / 2f) * g * 0.35f, ink, thick * 0.8f);
                }
                break;

            // Nuvem com três fios de chuva: a Primavera do degelo e da lama.
            case "chuva":
                Arc(0.38f, 0.42f, 0.16f, Mathf.Pi, Mathf.Tau);
                Arc(0.62f, 0.44f, 0.14f, Mathf.Pi, Mathf.Tau);
                Line(0.22f, 0.42f, 0.76f, 0.44f);
                foreach (float x in new[] { 0.30f, 0.50f, 0.70f }) Line(x, 0.56f, x - 0.06f, 0.86f, 0.85f);
                break;

            // Sol: o disco e os oito raios — o Verão das estradas secas.
            case "sol":
                Ring(0.50f, 0.50f, 0.20f, 1.1f);
                for (int i = 0; i < 8; i++)
                {
                    float a = Mathf.Tau * i / 8f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    ci.DrawLine(P(0.5f, 0.5f) + dir * 0.28f * u, P(0.5f, 0.5f) + dir * 0.44f * u, ink, thick);
                }
                break;

            // Folha caída com a nervura: o Outono.
            case "folha":
                Poly(0.50f, 0.10f, 0.82f, 0.42f, 0.50f, 0.78f, 0.18f, 0.42f, 0.50f, 0.10f);
                Line(0.50f, 0.14f, 0.50f, 0.92f, 0.9f);
                Line(0.50f, 0.34f, 0.32f, 0.40f, 0.7f);
                Line(0.50f, 0.34f, 0.68f, 0.40f, 0.7f);
                Line(0.50f, 0.52f, 0.34f, 0.56f, 0.7f);
                Line(0.50f, 0.52f, 0.66f, 0.56f, 0.7f);
                break;

            // Globo: a esfera com o equador e um meridiano — o mapa político.
            case "globo":
                Ring(0.50f, 0.50f, 0.38f, 1.1f);
                Line(0.12f, 0.50f, 0.88f, 0.50f, 0.85f);
                {
                    var c = P(0.50f, 0.50f);
                    var pts = new Vector2[25];
                    for (int k = 0; k < pts.Length; k++)
                    {
                        float t = Mathf.Tau * k / (pts.Length - 1);
                        pts[k] = c + new Vector2(Mathf.Cos(t) * 0.17f * u, Mathf.Sin(t) * 0.38f * u);
                    }
                    ci.DrawPolyline(pts, ink, thick * 0.85f);
                }
                break;

            // Caixa de mantimentos: o caixote com a cinta — o abastecimento.
            case "caixa":
                Poly(0.14f, 0.28f, 0.86f, 0.28f, 0.86f, 0.82f, 0.14f, 0.82f, 0.14f, 0.28f);
                Line(0.14f, 0.44f, 0.86f, 0.44f, 0.85f);
                Line(0.42f, 0.28f, 0.42f, 0.82f, 0.85f);
                Line(0.58f, 0.28f, 0.58f, 0.82f, 0.85f);
                break;

            // Punho erguido: a resistência da população ocupada.
            case "punho":
                Poly(0.30f, 0.86f, 0.30f, 0.46f, 0.36f, 0.34f, 0.64f, 0.34f, 0.72f, 0.46f, 0.72f, 0.86f, 0.30f, 0.86f);
                Line(0.30f, 0.56f, 0.72f, 0.56f, 0.85f);
                Line(0.42f, 0.34f, 0.42f, 0.22f, 0.85f);
                Line(0.54f, 0.34f, 0.54f, 0.20f, 0.85f);
                Line(0.66f, 0.36f, 0.70f, 0.24f, 0.85f);
                break;

            // Três figuras lado a lado: a população.
            case "gente":
                foreach (var (x, y, s) in new[] { (0.26f, 0.44f, 0.9f), (0.50f, 0.36f, 1f), (0.74f, 0.44f, 0.9f) })
                {
                    Dot(x, y, 0.10f * s);
                    Arc(x, y + 0.34f * s, 0.16f * s, Mathf.Pi, Mathf.Tau, 1.1f);
                    Line(x - 0.16f * s, y + 0.34f * s, x - 0.16f * s, y + 0.46f * s);
                    Line(x + 0.16f * s, y + 0.34f * s, x + 0.16f * s, y + 0.46f * s);
                }
                break;

            // Bomba a cair: o corpo e as quatro barbatanas — o apoio próximo.
            case "bomba":
                Poly(0.50f, 0.90f, 0.36f, 0.62f, 0.36f, 0.34f, 0.50f, 0.18f, 0.64f, 0.34f, 0.64f, 0.62f, 0.50f, 0.90f);
                Poly(0.36f, 0.40f, 0.22f, 0.24f, 0.30f, 0.44f);
                Poly(0.64f, 0.40f, 0.78f, 0.24f, 0.70f, 0.44f);
                Line(0.50f, 0.18f, 0.50f, 0.08f, 0.85f);
                break;

            // Alvo: três anéis e o ponto no meio — o bombardeamento.
            case "alvo":
                Ring(0.50f, 0.50f, 0.38f, 1.1f);
                Ring(0.50f, 0.50f, 0.24f);
                Dot(0.50f, 0.50f, 0.09f);
                break;

            // Luneta de vigia: o tubo em cone e o pé — a patrulha.
            case "luneta":
                Poly(0.14f, 0.70f, 0.24f, 0.52f, 0.80f, 0.24f, 0.90f, 0.38f, 0.30f, 0.74f, 0.14f, 0.70f);
                Line(0.34f, 0.66f, 0.34f, 0.90f, 0.85f);
                Line(0.22f, 0.90f, 0.48f, 0.90f, 0.85f);
                break;

            // Duas espadas cruzadas: a guerra. É a chapa mais antiga que há para isto e não é de ninguém.
            case "espadas":
                Line(0.16f, 0.84f, 0.82f, 0.18f, 1.15f);
                Line(0.84f, 0.84f, 0.18f, 0.18f, 1.15f);
                Line(0.16f, 0.64f, 0.36f, 0.84f);          // guarda da lâmina que sobe para a direita
                Line(0.84f, 0.64f, 0.64f, 0.84f);
                Dot(0.14f, 0.86f, 0.05f);
                Dot(0.86f, 0.86f, 0.05f);
                break;

            // Pomba de asa levantada: a paz.
            case "pomba":
                Fill(0.16f, 0.74f, 0.36f, 0.56f, 0.58f, 0.52f, 0.74f, 0.36f, 0.72f, 0.28f, 0.84f, 0.30f,
                     0.86f, 0.38f, 0.76f, 0.52f, 0.66f, 0.72f, 0.44f, 0.82f, 0.24f, 0.82f);
                Poly(0.40f, 0.58f, 0.52f, 0.32f, 0.64f, 0.56f);
                break;

            // Bandeira num mastro: a rendição. Sem cor nenhuma — a cor é da moldura, e branca já ela é.
            case "bandeira":
                Line(0.26f, 0.10f, 0.26f, 0.92f, 1.15f);
                Poly(0.26f, 0.16f, 0.86f, 0.16f, 0.86f, 0.52f, 0.26f, 0.52f);
                Line(0.16f, 0.92f, 0.38f, 0.92f);
                break;

            // Medalha pendurada na fita: a baixa no comando e o comandante que a leva.
            case "medalha":
                Line(0.36f, 0.10f, 0.44f, 0.42f);
                Line(0.64f, 0.10f, 0.56f, 0.42f);
                Line(0.36f, 0.10f, 0.64f, 0.10f);
                Ring(0.50f, 0.64f, 0.26f, 1.1f);
                Dot(0.50f, 0.64f, 0.08f);
                break;

            // Pórtico de colunas: a capital, o palácio do governo, a administração civil.
            case "coluna":
                Poly(0.10f, 0.34f, 0.50f, 0.12f, 0.90f, 0.34f);
                Line(0.10f, 0.34f, 0.90f, 0.34f, 1.1f);
                foreach (float x in new[] { 0.24f, 0.50f, 0.76f }) Line(x, 0.38f, x, 0.78f, 1.1f);
                Line(0.14f, 0.78f, 0.86f, 0.78f);
                Line(0.08f, 0.86f, 0.92f, 0.86f, 1.15f);
                break;

            // Coroa de três pontas sobre o aro: o domínio.
            case "coroa":
                Poly(0.14f, 0.72f, 0.14f, 0.30f, 0.32f, 0.48f, 0.50f, 0.20f, 0.68f, 0.48f, 0.86f, 0.30f,
                     0.86f, 0.72f, 0.14f, 0.72f);
                Line(0.12f, 0.80f, 0.88f, 0.80f, 1.15f);
                Dot(0.14f, 0.30f, 0.05f);
                Dot(0.50f, 0.20f, 0.055f);
                Dot(0.86f, 0.30f, 0.05f);
                break;

            // Duas mãos que se apertam: a aliança, e a ocupação entregue a gente da terra.
            case "aperto":
                Line(0.06f, 0.34f, 0.36f, 0.44f);
                Line(0.06f, 0.34f, 0.06f, 0.50f);
                Line(0.06f, 0.50f, 0.34f, 0.64f);
                Line(0.94f, 0.34f, 0.64f, 0.44f);
                Line(0.94f, 0.34f, 0.94f, 0.50f);
                Line(0.94f, 0.50f, 0.66f, 0.64f);
                Fill(0.36f, 0.42f, 0.64f, 0.42f, 0.66f, 0.64f, 0.34f, 0.64f);
                break;

            // Galhardete de cauda de andorinha: a honra de batalha que uma tropa ganha e passa a levar.
            case "fita":
                Poly(0.28f, 0.12f, 0.72f, 0.12f, 0.72f, 0.88f, 0.50f, 0.70f, 0.28f, 0.88f, 0.28f, 0.12f);
                Line(0.28f, 0.30f, 0.72f, 0.30f);
                Line(0.28f, 0.44f, 0.72f, 0.44f);
                break;

            // Três divisas: a promoção.
            case "galao":
                foreach (float y in new[] { 0.24f, 0.46f, 0.68f })
                    Poly(0.16f, y + 0.18f, 0.50f, y, 0.84f, y + 0.18f);
                break;

            // Taça de duas asas: o espólio de guerra.
            case "taca":
                Poly(0.28f, 0.16f, 0.72f, 0.16f, 0.68f, 0.48f, 0.50f, 0.58f, 0.32f, 0.48f, 0.28f, 0.16f);
                Arc(0.26f, 0.28f, 0.13f, Mathf.Pi * 0.5f, Mathf.Pi * 1.5f);
                Arc(0.74f, 0.28f, 0.13f, Mathf.Pi * 1.5f, Mathf.Pi * 2.5f);
                Line(0.50f, 0.58f, 0.50f, 0.76f, 1.15f);
                Line(0.38f, 0.76f, 0.62f, 0.76f);
                Line(0.30f, 0.86f, 0.70f, 0.86f, 1.2f);
                break;

            // Navio de vela e casco: o governo que embarca para o exílio.
            case "barco":
                Poly(0.08f, 0.62f, 0.92f, 0.62f, 0.78f, 0.84f, 0.22f, 0.84f, 0.08f, 0.62f);
                Line(0.50f, 0.62f, 0.50f, 0.16f, 1.1f);
                Poly(0.50f, 0.22f, 0.80f, 0.44f, 0.50f, 0.52f);
                Line(0.14f, 0.72f, 0.86f, 0.72f);
                break;

            // Estrela de estilhaços: a sabotagem — o rebentamento na retaguarda.
            case "estilhaco":
                for (int i = 0; i < 8; i++)
                {
                    float a = Mathf.Tau * i / 8f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    ci.DrawLine(P(0.5f, 0.5f) + dir * 0.14f * u,
                                P(0.5f, 0.5f) + dir * (i % 2 == 0 ? 0.46f : 0.30f) * u, ink, thick * 1.1f);
                }
                Ring(0.50f, 0.50f, 0.12f);
                break;

            // Pasta de despachos: o gabinete civil e as cadeiras dele.
            case "pasta":
                Poly(0.12f, 0.34f, 0.88f, 0.34f, 0.88f, 0.84f, 0.12f, 0.84f, 0.12f, 0.34f);
                Arc(0.50f, 0.34f, 0.16f, Mathf.Pi, Mathf.Tau);
                Line(0.12f, 0.54f, 0.88f, 0.54f);
                Fill(0.44f, 0.48f, 0.56f, 0.48f, 0.56f, 0.60f, 0.44f, 0.60f);
                break;

            // Três elos: os prisioneiros e o trabalho forçado.
            case "corrente":
                Ring(0.26f, 0.28f, 0.15f, 1.1f);
                Ring(0.50f, 0.50f, 0.15f, 1.1f);
                Ring(0.74f, 0.72f, 0.15f, 1.1f);
                break;

            // Balança de dois pratos: a lei nacional.
            case "balanca":
                Line(0.50f, 0.14f, 0.50f, 0.80f, 1.15f);
                Line(0.12f, 0.30f, 0.88f, 0.30f, 1.15f);
                Line(0.30f, 0.86f, 0.70f, 0.86f, 1.15f);
                Line(0.12f, 0.30f, 0.02f, 0.52f);
                Line(0.12f, 0.30f, 0.22f, 0.52f);
                Arc(0.12f, 0.52f, 0.10f, 0f, Mathf.Pi);
                Line(0.88f, 0.30f, 0.78f, 0.52f);
                Line(0.88f, 0.30f, 0.98f, 0.52f);
                Arc(0.88f, 0.52f, 0.10f, 0f, Mathf.Pi);
                break;

            // Caveira: o revés e o comandante que não volta.
            case "caveira":
                Arc(0.50f, 0.50f, 0.30f, Mathf.Pi, Mathf.Tau, 1.15f);
                Poly(0.20f, 0.50f, 0.20f, 0.66f, 0.32f, 0.72f, 0.32f, 0.86f, 0.68f, 0.86f, 0.68f, 0.72f,
                     0.80f, 0.66f, 0.80f, 0.50f);
                Dot(0.38f, 0.50f, 0.08f);
                Dot(0.62f, 0.50f, 0.08f);
                Poly(0.50f, 0.58f, 0.44f, 0.68f, 0.56f, 0.68f, 0.50f, 0.58f);
                break;

            // Megafone com o som a sair: a propaganda.
            case "megafone":
                Poly(0.10f, 0.40f, 0.10f, 0.60f, 0.44f, 0.74f, 0.44f, 0.26f, 0.10f, 0.40f);
                Line(0.18f, 0.58f, 0.18f, 0.82f, 1.1f);
                Arc(0.50f, 0.50f, 0.14f, -Mathf.Pi * 0.4f, Mathf.Pi * 0.4f);
                Arc(0.50f, 0.50f, 0.30f, -Mathf.Pi * 0.4f, Mathf.Pi * 0.4f);
                break;

            // Penso: o ferimento ligeiro, o que tira o comandante de serviço por uns dias e mais nada.
            case "penso":
                Poly(0.14f, 0.44f, 0.44f, 0.14f, 0.86f, 0.56f, 0.56f, 0.86f, 0.14f, 0.44f);
                Line(0.32f, 0.26f, 0.74f, 0.68f);
                Line(0.26f, 0.32f, 0.68f, 0.74f);
                Dot(0.42f, 0.48f, 0.035f);
                Dot(0.50f, 0.40f, 0.035f);
                Dot(0.58f, 0.52f, 0.035f);
                Dot(0.50f, 0.60f, 0.035f);
                break;

            // Gota: o ferido em combate.
            case "gota":
                Poly(0.50f, 0.10f, 0.76f, 0.48f, 0.76f, 0.64f, 0.50f, 0.88f, 0.24f, 0.64f, 0.24f, 0.48f, 0.50f, 0.10f);
                Arc(0.50f, 0.62f, 0.14f, Mathf.Pi * 0.85f, Mathf.Pi * 1.55f);
                break;

            // Cruz de socorro: o ferido com gravidade, o que sai da guerra por meses.
            case "cruz":
                Poly(0.38f, 0.14f, 0.62f, 0.14f, 0.62f, 0.38f, 0.86f, 0.38f, 0.86f, 0.62f, 0.62f, 0.62f,
                     0.62f, 0.86f, 0.38f, 0.86f, 0.38f, 0.62f, 0.14f, 0.62f, 0.14f, 0.38f, 0.38f, 0.38f, 0.38f, 0.14f);
                break;

            // Pára-quedas: o comandante de asa abatido sobre o inimigo.
            case "paraquedas":
                Arc(0.50f, 0.46f, 0.36f, Mathf.Pi, Mathf.Tau, 1.15f);
                Line(0.14f, 0.46f, 0.86f, 0.46f);
                Line(0.14f, 0.46f, 0.46f, 0.72f);
                Line(0.50f, 0.46f, 0.50f, 0.72f);
                Line(0.86f, 0.46f, 0.54f, 0.72f);
                Dot(0.50f, 0.80f, 0.08f);
                break;

            // Vagas: o comandante de esquadra afundado com o navio.
            case "onda":
                foreach (float y in new[] { 0.32f, 0.52f, 0.72f })
                {
                    Arc(0.30f, y, 0.20f, Mathf.Pi, Mathf.Tau);
                    Arc(0.70f, y, 0.20f, 0f, Mathf.Pi);
                }
                break;

            // Cofre do tesouro: a porta blindada com o disco do segredo. É o dinheiro do país — vai no
            // primeiro mostrador da barra de cima, onde antes estava um cifrão emprestado a outra moeda.
            case "cofre":
                Poly(0.12f, 0.18f, 0.88f, 0.18f, 0.88f, 0.80f, 0.12f, 0.80f, 0.12f, 0.18f);
                Poly(0.22f, 0.26f, 0.78f, 0.26f, 0.78f, 0.72f, 0.22f, 0.72f, 0.22f, 0.26f);
                Ring(0.50f, 0.49f, 0.13f);
                Dot(0.50f, 0.49f, 0.035f);
                Line(0.50f, 0.49f, 0.50f, 0.33f, 0.85f);
                Line(0.50f, 0.49f, 0.63f, 0.57f, 0.85f);
                Line(0.22f, 0.80f, 0.22f, 0.90f, 0.85f);
                Line(0.78f, 0.80f, 0.78f, 0.90f, 0.85f);
                break;

            // Barril de combustível: o tambor de pé, com os dois aros. Os tampos são elipses e não círculos
            // — um tambor desenhado com círculos lê-se como lata de conserva —, por isso vão à mão.
            case "barril":
            {
                void Rim(float cy, float half)
                {
                    var prev = P(0.72f, cy);
                    for (int i = 1; i <= 24; i++)
                    {
                        float a = Mathf.Tau * i / 24f;
                        var next = P(0.50f + 0.22f * Mathf.Cos(a), cy + half * Mathf.Sin(a));
                        ci.DrawLine(prev, next, ink, thick);
                        prev = next;
                    }
                }
                Line(0.28f, 0.22f, 0.28f, 0.80f, 1.1f);
                Line(0.72f, 0.22f, 0.72f, 0.80f, 1.1f);
                Rim(0.22f, 0.08f);
                Rim(0.80f, 0.08f);
                Line(0.28f, 0.42f, 0.72f, 0.42f, 0.85f);
                Line(0.28f, 0.60f, 0.72f, 0.60f, 0.85f);
                break;
            }

            // A leva do chão: seis terrenos, para a região se ver antes de se ler. A convenção é a dos mapas
            // militares — silhueta de perfil, sem cor nenhuma, que a cor já é a da região no mapa.

            // Planície: a linha do horizonte, dois regos e o restolho. O chão que não custa nada.
            case "campo":
                Line(0.10f, 0.62f, 0.90f, 0.62f, 1.15f);
                Line(0.14f, 0.74f, 0.86f, 0.74f, 0.8f);
                Line(0.20f, 0.84f, 0.80f, 0.84f, 0.8f);
                Line(0.30f, 0.62f, 0.30f, 0.50f, 0.8f);
                Line(0.50f, 0.62f, 0.50f, 0.44f, 0.8f);
                Line(0.70f, 0.62f, 0.70f, 0.52f, 0.8f);
                break;

            // Floresta: duas coníferas de perfil, a da frente maior — é a silhueta que lê como mata.
            case "arvore":
                Poly(0.34f, 0.86f, 0.34f, 0.74f);
                Poly(0.16f, 0.74f, 0.34f, 0.44f, 0.52f, 0.74f, 0.16f, 0.74f);
                Poly(0.20f, 0.58f, 0.34f, 0.34f, 0.48f, 0.58f);
                Poly(0.70f, 0.86f, 0.70f, 0.78f);
                Poly(0.56f, 0.78f, 0.70f, 0.54f, 0.84f, 0.78f, 0.56f, 0.78f);
                Poly(0.60f, 0.64f, 0.70f, 0.46f, 0.80f, 0.64f);
                break;

            // Urbano: três prédios encostados, o do meio mais alto, com as janelas a marcar a escala.
            case "cidade":
                Poly(0.10f, 0.86f, 0.10f, 0.54f, 0.34f, 0.54f, 0.34f, 0.86f);
                Poly(0.38f, 0.86f, 0.38f, 0.26f, 0.62f, 0.26f, 0.62f, 0.86f);
                Poly(0.66f, 0.86f, 0.66f, 0.46f, 0.90f, 0.46f, 0.90f, 0.86f);
                Line(0.08f, 0.86f, 0.92f, 0.86f, 1.1f);
                Line(0.44f, 0.38f, 0.56f, 0.38f, 0.75f);
                Line(0.44f, 0.52f, 0.56f, 0.52f, 0.75f);
                Line(0.44f, 0.66f, 0.56f, 0.66f, 0.75f);
                Line(0.16f, 0.66f, 0.28f, 0.66f, 0.75f);
                Line(0.72f, 0.60f, 0.84f, 0.60f, 0.75f);
                break;

            // Montanha: dois picos, o maior com a neve marcada por dentro — o chão que custa o dobro.
            case "montanha":
                Poly(0.06f, 0.80f, 0.38f, 0.24f, 0.70f, 0.80f);
                Poly(0.28f, 0.42f, 0.38f, 0.24f, 0.48f, 0.42f);
                Poly(0.44f, 0.80f, 0.68f, 0.44f, 0.92f, 0.80f);
                Line(0.04f, 0.80f, 0.94f, 0.80f, 1.1f);
                break;

            // Deserto: duas dunas e o sol baixo. Sem palmeira — a palmeira é postal, a duna é terreno.
            case "duna":
                Arc(0.34f, 0.92f, 0.26f, Mathf.Pi, Mathf.Tau, 1.1f);
                Arc(0.70f, 0.96f, 0.22f, Mathf.Pi, Mathf.Tau, 1.1f);
                Ring(0.72f, 0.36f, 0.13f);
                Line(0.10f, 0.86f, 0.90f, 0.86f, 0.8f);
                break;

            // Tundra: a placa de gelo partida — a linha da água, a fenda e o bloco levantado.
            case "gelo":
                Line(0.08f, 0.70f, 0.92f, 0.70f, 1.15f);
                Poly(0.14f, 0.70f, 0.30f, 0.46f, 0.46f, 0.70f);
                Poly(0.52f, 0.70f, 0.64f, 0.56f, 0.76f, 0.70f);
                Line(0.12f, 0.82f, 0.44f, 0.82f, 0.8f);
                Line(0.54f, 0.82f, 0.88f, 0.82f, 0.8f);
                Line(0.20f, 0.92f, 0.80f, 0.92f, 0.8f);
                break;

            // Roda dentada: a peça neutra de quem não tem chapa própria.
            default:
                Ring(0.50f, 0.50f, 0.28f, 1.1f);
                Ring(0.50f, 0.50f, 0.12f);
                for (int i = 0; i < 8; i++)
                {
                    float a = Mathf.Tau * i / 8f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    ci.DrawLine(P(0.5f, 0.5f) + dir * 0.28f * u, P(0.5f, 0.5f) + dir * 0.42f * u, ink, thick * 1.2f);
                }
                break;
        }
    }

    /// <summary>Todos os nomes de chapa que a base de dados pede hoje, sem repetidos: obras, ramos da
    /// árvore, estações, modos de mapa, missões de ar e mar, géneros da crónica, pastas do gabinete,
    /// políticas de ocupação, gravidades de baixa e números da ficha de combate. `extra` é para os poucos sítios que não são linha de
    /// tabela nenhuma (a infra-estrutura e a fortificação do menu de construir, que são regras).
    /// Serve o contador do --smoke: é a lista que se compara com o que o Glyph sabe mesmo desenhar.</summary>
    public static List<string> Asked(World w, params string[] extra)
        => w.BuildingDefs.Values.Select(d => d.Glyph)
            .Concat(w.TechBranches.Values.Select(b => b.Glyph))
            .Concat(w.SeasonDefs.Values.Select(s => s.Glyph))
            .Concat(w.MapModeDefs.Values.Select(m => m.Glyph))
            .Concat(w.AirMissionDefs.Values.Select(m => m.Glyph))
            .Concat(w.NavalMissionDefs.Values.Select(m => m.Glyph))
            .Concat(w.ChronicleKinds.Values.Select(k => k.Glyph))
            .Concat(w.CabinetSlots.Select(c => c.Glyph))
            .Concat(w.OccupationPolicyDefs.Values.Select(o => o.Glyph))
            .Concat(w.WoundKinds.Values.Select(k => k.Glyph))
            .Concat(w.UnitStatDefs.Values.Select(s => s.Glyph))
            .Concat(w.TerrainDefs.Values.Select(t => t.Glyph))
            .Concat(extra)
            .Distinct().ToList();

    /// <summary>Cola uma chapa dentro de um botão, encostada à esquerda e ao meio da altura. Um Button não
    /// arruma filhos, por isso a chapa é ancorada à mão — e o texto do botão tem de começar com um recuo do
    /// tamanho dela para não lhe ir por cima. A tinta é do lado de quem chama: um botão aceso tem fundo
    /// claro e pede tinta escura.</summary>
    public static Plate Stamp(Button b, string name, Color ink, float size = 18f)
    {
        var p = Make(name, size, ink);
        p.MouseFilter = Control.MouseFilterEnum.Ignore;
        p.AnchorTop = p.AnchorBottom = 0.5f;
        p.OffsetLeft = 8; p.OffsetRight = 8 + size;
        p.OffsetTop = -size / 2f; p.OffsetBottom = size / 2f;
        b.AddChild(p);
        return p;
    }

    /// <summary>Varre uma sub-árvore de nós e conta as chapas — e quantas dessas saíram como roda dentada
    /// por o nome pedido não existir. É o que os contadores do --smoke usam para não dizerem que desenharam
    /// o que não desenharam: um nome mal escrito na tabela não dá erro, dá uma roda calada.</summary>
    public static (int Drawn, int FellBack) Count(Node root)
    {
        int drawn = 0, fell = 0;
        Walk(root);
        return (drawn, fell);

        void Walk(Node n)
        {
            if (n is Plate p) { drawn++; if (p.FellBack) fell++; }
            foreach (var ch in n.GetChildren()) Walk(ch);
        }
    }

    /// <summary>Um nó que desenha a chapa e mais nada. Entra onde antes entrava um Label com um emoji.</summary>
    public static Plate Make(string name, float size, Color? ink = null, string? tip = null)
    {
        var p = new Plate { GlyphName = name, Ink = ink ?? Ui.Accent, CustomMinimumSize = new Vector2(size, size) };
        if (tip is not null) p.TooltipText = tip;
        return p;
    }

    /// <summary>O nó. Guarda o nome para o contador do smoke poder varrer o ecrã e dizer quantas chapas
    /// estão mesmo desenhadas naquele instante — e quantas dessas caíram na roda por falta de desenho.</summary>
    public sealed partial class Plate : Control
    {
        public string GlyphName { get; set; } = "roda";
        public Color Ink { get; set; } = Colors.White;
        public bool FellBack => !Knows(GlyphName);

        public override void _Draw() => Glyph.Draw(this, new Rect2(Vector2.Zero, Size), GlyphName, Ink);
    }
}
