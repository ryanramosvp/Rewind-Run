using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cartão de dica no alto da tela, para apresentar uma mecânica nova no
/// lugar em que ela aparece pela primeira vez.
///
/// Uma frase, poucos segundos, e some. As fases novas ensinam pelo próprio
/// desenho - o primeiro vão de impulso tem chão embaixo, a primeira zona tem
/// uma hélice óbvia dentro -, e a dica só dá o nome do botão. Um texto longo
/// no meio do percurso seria lido parado, que é o oposto do jogo.
///
/// O botão escrito acompanha o último aparelho usado, como no indicador do
/// rewind.
///
/// Anima com tempo sem escala, para não congelar com a pausa no meio do aviso.
/// </summary>
public class PainelDeAvisos : MonoBehaviour
{
    public static PainelDeAvisos Instancia { get; private set; }

    [SerializeField] CanvasGroup grupo;
    [SerializeField] TMP_Text texto;
    [SerializeField, Min(0.05f)] float fade = 0.25f;

    string _teclado, _controle;
    float _restante;

    void Awake()
    {
        Instancia = this;
        if (grupo != null) grupo.alpha = 0f;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    /// <summary>Mostra um aviso por alguns segundos, com o texto do aparelho em uso.</summary>
    public static void Mostrar(string teclado, string controle, float segundos)
    {
        if (Instancia == null) return;
        Instancia._teclado = teclado;
        Instancia._controle = string.IsNullOrEmpty(controle) ? teclado : controle;
        Instancia._restante = segundos;
        Instancia.Escrever();
    }

    void Update()
    {
        if (grupo == null) return;

        float dt = Time.unscaledDeltaTime;
        _restante -= dt;
        float alvo = _restante > 0f ? 1f : 0f;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, alvo, dt / fade);

        if (_restante > 0f) Escrever();
    }

    void Escrever()
    {
        if (texto == null) return;

        double pad = Gamepad.current != null ? Gamepad.current.lastUpdateTime : -1.0;
        double teclas = System.Math.Max(Keyboard.current != null ? Keyboard.current.lastUpdateTime : -1.0,
                                        Mouse.current != null ? Mouse.current.lastUpdateTime : -1.0);
        texto.text = pad > teclas ? _controle : _teclado;
    }
}
