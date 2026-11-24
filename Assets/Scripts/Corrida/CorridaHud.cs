using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Medidor da corrida: uma barra no alto da tela com a bruxa e o fantasma
/// marcados, e quantos segundos de vantagem ou de atraso ela tem.
///
/// A diferença é medida em tempo, e não em metros: "1,4 s à frente" diz o
/// quanto se pode errar, e é a moeda do jogo inteiro. A conta é quanto
/// tempo o fantasma ainda leva para chegar onde a bruxa está (ou quanto já
/// faz que ele passou por ali).
/// </summary>
public class CorridaHud : MonoBehaviour
{
    [SerializeField] RectTransform trilho;
    [SerializeField] RectTransform marcaDaBruxa;
    [SerializeField] RectTransform marcaDoFantasma;
    [SerializeField] TMP_Text diferenca;
    [SerializeField] CanvasGroup grupo;

    [SerializeField] Color corNaFrente = new Color(0.55f, 1f, 0.6f);
    [SerializeField] Color corAtras = new Color(1f, 0.5f, 0.45f);

    PlayerController _bruxa;
    int _dica;

    void Start()
    {
        _bruxa = FindAnyObjectByType<PlayerController>();
        var respawn = LevelRespawn.Instance;
        if (respawn != null) respawn.Died += () => _dica = 0;
    }

    void Update()
    {
        var fantasma = FantasmaDaCorrida.Instancia;
        if (fantasma == null || _bruxa == null || trilho == null) return;

        if (grupo != null)
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, fantasma.Parado ? 0f : 1f, Time.unscaledDeltaTime * 2f);

        float total = Mathf.Max(1f, fantasma.DistanciaTotal);
        float daBruxa = fantasma.DistanciaNaRota(_bruxa.transform.position, ref _dica);
        float doFantasma = fantasma.DistanciaDoFantasma;

        float largura = trilho.rect.width;
        Posicionar(marcaDaBruxa, daBruxa / total * largura);
        Posicionar(marcaDoFantasma, doFantasma / total * largura);

        if (diferenca == null || fantasma.Parado) return;
        float vantagem = fantasma.TempoNaDistancia(daBruxa) - fantasma.TempoDaCorrida;
        diferenca.text = vantagem >= 0f
            ? $"{vantagem:0.0} s À FRENTE"
            : $"{-vantagem:0.0} s ATRÁS";
        diferenca.color = vantagem >= 0f ? corNaFrente : corAtras;
    }

    static void Posicionar(RectTransform marca, float x)
    {
        if (marca == null) return;
        marca.anchoredPosition = new Vector2(x, marca.anchoredPosition.y);
    }
}
