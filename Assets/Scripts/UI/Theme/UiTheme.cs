using TMPro;
using UnityEngine;

/// <summary>
/// A identidade visual da interface num asset só: cores, fonte, materiais de
/// texto e as peças de sprite.
///
/// Pedra vulcânica escura no fundo, metal gasto nas molduras, laranja de brasa
/// e dourado só no que está selecionado ou importa agora. A regra é essa
/// economia: se tudo brilha, nada se destaca - e o que precisa se destacar é a
/// opção escolhida e o número que o jogador está perseguindo.
///
/// Os componentes visuais (<see cref="MenuButtonView"/> e companhia) leem daqui
/// em tempo de jogo; os construtores de cena, ao montar a interface. Trocar uma
/// cor aqui muda o jogo inteiro.
/// </summary>
[CreateAssetMenu(menuName = "Rewind Run/Tema da Interface", fileName = "RewindRunTheme")]
public class UiTheme : ScriptableObject
{
    [Header("Pedra")]
    public Color basalt = new Color(0.086f, 0.063f, 0.055f);
    public Color stone = new Color(0.20f, 0.15f, 0.13f);
    public Color stoneLit = new Color(0.31f, 0.21f, 0.16f);

    [Header("Metal")]
    public Color metal = new Color(0.45f, 0.42f, 0.40f);
    public Color metalDark = new Color(0.26f, 0.24f, 0.23f);

    [Header("Fogo")]
    public Color ember = new Color(0.953f, 0.420f, 0.133f);
    public Color molten = new Color(1f, 0.64f, 0.22f);
    public Color gold = new Color(0.93f, 0.71f, 0.33f);
    public Color danger = new Color(0.90f, 0.27f, 0.20f);

    [Header("Texto")]
    public Color sand = new Color(0.95f, 0.87f, 0.74f);
    public Color ash = new Color(0.80f, 0.78f, 0.75f);
    public Color highlightText = new Color(1f, 0.96f, 0.86f);

    [Header("Fonte")]
    public TMP_FontAsset font;
    [Tooltip("Contorno escuro e sombra: logotipo e números grandes.")]
    public Material titleMaterial;
    [Tooltip("Só uma sombra curta: rótulos sobre pedra.")]
    public Material labelMaterial;

    [Header("Sprites (9-slice)")]
    public Sprite stonePanel;
    public Sprite metalFrame;
    public Sprite glow;
    public Sprite shade;
    public Sprite rewindIcon;
    public Sprite diamond;

    [Header("Movimento")]
    [Tooltip("Quanto o botão cresce selecionado.")]
    public float selectedScale = 1.04f;
    [Tooltip("Quanto ele afunda enquanto apertado.")]
    public float pressedScale = 0.96f;
    [Tooltip("Velocidade das transições, em 1/s. Mais alto, mais seco.")]
    public float transitionSpeed = 14f;
}
