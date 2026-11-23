using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Créditos: uma coluna de texto que sobe devagar sobre o vulcão do menu.
///
/// Tudo em tempo sem escala, porque os créditos podem ser abertos com o jogo
/// parado (timeScale zero) e não podem congelar.
///
/// Segurar Enter, Espaço ou A acelera a rolagem, em vez de pular de uma vez:
/// quem quer chegar ao fim chega, e ainda passa os olhos pelos nomes. Esc ou
/// B voltam ao menu a qualquer momento, e um botão no canto faz o mesmo para
/// quem está no mouse. No fim a rolagem para no "Obrigado por jogar".
///
/// O botão nunca fica selecionado: Enter e A são também o "confirmar" da
/// interface, e com ele escolhido acelerar a rolagem sairia dos créditos.
///
/// Há duas entradas. Pelo menu, a saída volta ao menu e a rolagem para no
/// fim. Depois da última fase (<see cref="AbrirNoFimDaCampanha"/>), a saída
/// vai para os resultados da campanha, e vai sozinha alguns segundos depois
/// do "Obrigado por jogar": o jogo termina sem pedir um botão.
/// </summary>
public class TelaDeCreditos : MonoBehaviour
{
    [Tooltip("A coluna que sobe. Começa logo abaixo da tela.")]
    [SerializeField] RectTransform coluna;

    [Tooltip("Entrada da tela inteira, de transparente a visível.")]
    [SerializeField] CanvasGroup entrada;

    [Tooltip("Velocidade da rolagem, em unidades da tela (referência de 1080 de altura) por segundo.")]
    [SerializeField, Min(1f)] float velocidade = 50f;

    [Tooltip("Quantas vezes mais rápido enquanto se segura o botão de acelerar.")]
    [SerializeField, Min(1f)] float acelerado = 5f;

    [Tooltip("Onde a coluna para: posição Y final dela.")]
    [SerializeField] float paradaFinal = 1800f;

    [SerializeField] Button voltar;
    [SerializeField] string cenaDoMenu = "Menu";

    [Tooltip("Rótulo do botão de saída, trocado quando a saída leva aos resultados.")]
    [SerializeField] TMP_Text rotuloDoBotao;

    [Tooltip("Linha de comandos no rodapé, idem.")]
    [SerializeField] TMP_Text dica;

    [Tooltip("Segundos parado no fim antes de seguir sozinho, quando vindo da campanha.")]
    [SerializeField, Min(0f)] float esperaNoFim = 4f;

    // Estático porque atravessa a troca de cena: quem pede é a cena anterior.
    static string s_ProximaCena;

    float _y;
    float _inicioY;
    string _saida;
    float _parado;

    /// <summary>Os próximos créditos saem para <paramref name="cena"/> em vez do menu, e sozinhos no fim.</summary>
    public static void AbrirNoFimDaCampanha(string cena) => s_ProximaCena = cena;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (coluna != null)
        {
            _inicioY = coluna.anchoredPosition.y;
            _y = _inicioY;
        }

        if (entrada != null) entrada.alpha = 0f;

        _saida = s_ProximaCena;
        s_ProximaCena = null;
        if (_saida != null)
        {
            if (rotuloDoBotao != null) rotuloDoBotao.text = "CONTINUAR";
            if (dica != null) dica.text = "SEGURE ENTER / A  acelerar      ESC / B  pular";
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        var teclado = Keyboard.current;
        var controle = Gamepad.current;

        if ((teclado != null && teclado.escapeKey.wasPressedThisFrame) ||
            (controle != null && controle.buttonEast.wasPressedThisFrame))
        {
            VoltarAoMenu();
            return;
        }

        if (entrada != null) entrada.alpha = Mathf.MoveTowards(entrada.alpha, 1f, dt / 1.2f);

        bool acelerando = (teclado != null && (teclado.enterKey.isPressed || teclado.spaceKey.isPressed)) ||
                          (controle != null && controle.buttonSouth.isPressed);

        if (coluna != null && _y < paradaFinal)
        {
            _y = Mathf.Min(paradaFinal, _y + velocidade * (acelerando ? acelerado : 1f) * dt);
            coluna.anchoredPosition = new Vector2(coluna.anchoredPosition.x, _y);
        }
        else if (_saida != null)
        {
            _parado += dt;
            if (_parado >= esperaNoFim) VoltarAoMenu();
        }
    }

    /// <summary>Sai dos créditos: para o menu ou, no fim da campanha, para os resultados.</summary>
    public void VoltarAoMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(_saida ?? cenaDoMenu);
    }
}
