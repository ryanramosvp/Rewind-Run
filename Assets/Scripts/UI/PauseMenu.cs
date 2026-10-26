using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pausa a corrida com Esc.
///
/// Não precisa congelar o cronômetro à mão: <see cref="GameSession"/> mede com
/// Time.time, que é tempo escalado e para junto com o timeScale. O mesmo vale
/// para o rewind, cujas gravações acontecem no passo de física - que não roda
/// com o jogo parado.
///
/// Quem manda no Esc, quando a tela de fim está aberta, é ela. Duas telas
/// disputando a mesma tecla acabariam abrindo a pausa por cima da vitória.
///
/// Abre com Esc ou com P. O P não é capricho: dentro do Editor do Unity o Esc
/// é a tecla que solta o cursor preso, e nessas horas ele não chega ao jogo.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject panel;

    [Tooltip("Primeiro botão a receber foco, para o teclado funcionar sem mouse.")]
    [SerializeField] Button firstButton;

    [SerializeField] string menuScene = "Menu";

    [SerializeField] Slider volumeSlider;
    [SerializeField] TMP_Text volumeLabel;

    EndScreen _endScreen;
    bool _paused;

    /// <summary>A corrida está parada agora?</summary>
    public bool IsPaused => _paused;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);

        // Time.timeScale é global e sobrevive a trocas de cena. Se a fase
        // anterior foi deixada pausada, começar sem zerar deixaria esta parada.
        Time.timeScale = 1f;
    }

    void Start() => _endScreen = FindAnyObjectByType<EndScreen>();

    void Update()
    {
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;

        // P além de Esc. No Editor do Unity o Esc é reservado para soltar o
        // cursor quando ele está preso, e nesse caso não chega ao jogo - o que
        // deixaria a pausa intestável justo onde ela é testada. Na build o Esc
        // funciona normalmente; o P existe para o Editor e não atrapalha.
        bool pedido = (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame
                                            || keyboard.pKey.wasPressedThisFrame))
                      || (pad != null && pad.startButton.wasPressedThisFrame);

        // Com a pausa aberta, o B do controle também volta a correr.
        if (_paused && pad != null && pad.buttonEast.wasPressedThisFrame) pedido = true;

        if (!pedido) return;

        if (_endScreen != null && _endScreen.IsShowing) return;

        if (_paused) Resume();
        else Pause();
    }

    public void Pause()
    {
        _paused = true;

        if (panel != null) panel.SetActive(true);
        Time.timeScale = 0f;

        // Sem isto os botões não são clicáveis quando a câmera prende o cursor.
        OrbitCamera.FreeCursor();

        RefreshVolume();

        if (firstButton != null) firstButton.Select();
    }

    /// <summary>Chamada pela barra de volume. Pública para o Inspector enxergar.</summary>
    public void OnVolumeChanged(float value)
    {
        GameSettings.Volume = value;
        RefreshLabel();
    }

    void RefreshVolume()
    {
        // Sem notificar: a tela abre lendo a preferência, não escrevendo nela.
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        RefreshLabel();
    }

    void RefreshLabel()
    {
        if (volumeLabel != null)
            volumeLabel.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
    }

    public void Resume()
    {
        _paused = false;

        if (panel != null) panel.SetActive(false);
        Time.timeScale = 1f;

        OrbitCamera.RestoreCursor();
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuScene);
    }
}
