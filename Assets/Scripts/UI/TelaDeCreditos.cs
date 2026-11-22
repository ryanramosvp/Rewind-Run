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
/// </summary>
public class TelaDeCreditos : MonoBehaviour
{
    [Tooltip("A coluna que sobe. Começa logo abaixo da tela.")]
    [SerializeField] RectTransform coluna;

    [Tooltip("Entrada da tela inteira, de transparente a visível.")]
    [SerializeField] CanvasGroup entrada;

    [Tooltip("Velocidade da rolagem, em unidades da tela (referência de 1080 de altura) por segundo.")]
    [SerializeField, Min(1f)] float velocidade = 70f;

    [Tooltip("Quantas vezes mais rápido enquanto se segura o botão de acelerar.")]
    [SerializeField, Min(1f)] float acelerado = 5f;

    [Tooltip("Onde a coluna para: posição Y final dela.")]
    [SerializeField] float paradaFinal = 1800f;

    [SerializeField] Button voltar;
    [SerializeField] string cenaDoMenu = "Menu";

    float _y;
    float _inicioY;

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
    }

    public void VoltarAoMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(cenaDoMenu);
    }
}
