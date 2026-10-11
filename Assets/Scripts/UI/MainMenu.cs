using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Botões do menu inicial.
///
/// O painel de controles esconde os botões em vez de abrir por cima deles.
/// Sobrepostos, o painel cobria o próprio botão que o fecharia, e sem tecla de
/// saída o menu virava um beco sem saída.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Cena da fase a carregar em Jogar.")]
    [SerializeField] string levelScene = "Level_CorridaDeLava";

    [SerializeField] GameObject buttonsGroup;
    [SerializeField] GameObject controlsPanel;
    [SerializeField] GameObject settingsPanel;
    [SerializeField] Button firstButton;
    [SerializeField] Button backButton;
    [SerializeField] Button settingsBackButton;
    [SerializeField] Text timerToggleLabel;
    [SerializeField] Text speedToggleLabel;
    [SerializeField] Image timerPill;
    [SerializeField] Image speedPill;
    [SerializeField] Slider volumeSlider;
    [SerializeField] Text volumeLabel;

    static readonly Color OnColor = new Color(0f, 0.518f, 0.329f);
    static readonly Color OffColor = new Color(0.29f, 0.32f, 0.33f);

    void Start()
    {
        // O menu é o único lugar do jogo com cursor; em jogo ele fica preso.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        CloseControls();
    }

    void Update()
    {
        bool open = (controlsPanel != null && controlsPanel.activeSelf)
                    || (settingsPanel != null && settingsPanel.activeSelf);

        if (!open) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Esc sempre fecha, mesmo que o botão não esteja visível por qualquer
        // motivo. É a rede de segurança contra ficar preso numa tela.
        if (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
            CloseControls();
    }

    public void Play() => SceneManager.LoadScene(levelScene);

    public void OpenControls()
    {
        if (controlsPanel != null) controlsPanel.SetActive(true);
        if (buttonsGroup != null) buttonsGroup.SetActive(false);

        // Deixa um botão selecionado para teclado e controle funcionarem sem
        // precisar clicar primeiro.
        if (backButton != null) backButton.Select();
    }

    public void CloseControls()
    {
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (buttonsGroup != null) buttonsGroup.SetActive(true);

        if (firstButton != null) firstButton.Select();
    }

    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (buttonsGroup != null) buttonsGroup.SetActive(false);

        RefreshToggleLabels();

        if (settingsBackButton != null) settingsBackButton.Select();
    }

    public void ToggleTimer()
    {
        GameSettings.ShowTimer = !GameSettings.ShowTimer;
        RefreshToggleLabels();
    }

    public void ToggleSpeed()
    {
        GameSettings.ShowSpeed = !GameSettings.ShowSpeed;
        RefreshToggleLabels();
    }

    /// <summary>
    /// O rótulo mostra o estado atual, não a ação.
    ///
    /// "CRONOMETRO: LIGADO" diz o que está valendo agora; "DESLIGAR
    /// CRONOMETRO" diria o que vai acontecer. O primeiro se lê sem pensar, e
    /// num menu de opções o que importa é conferir a configuração, não a ação.
    /// </summary>
    /// <summary>
    /// Chamada pela barra de volume.
    ///
    /// Pública porque é o Inspector que a liga ao evento da barra, e o Unity só
    /// enxerga métodos públicos ao serializar esse vínculo.
    /// </summary>
    public void OnVolumeChanged(float value)
    {
        GameSettings.Volume = value;
        RefreshVolumeLabel();
    }

    void RefreshVolumeLabel()
    {
        if (volumeLabel != null)
            volumeLabel.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
    }

    void RefreshToggleLabels()
    {
        // SetValueWithoutNotify, e nao value: atribuir direto dispararia o
        // evento da barra, que gravaria a preferencia de volta por cima dela
        // mesma toda vez que a tela abrisse.
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        RefreshVolumeLabel();

        Apply(timerToggleLabel, timerPill, GameSettings.ShowTimer);
        Apply(speedToggleLabel, speedPill, GameSettings.ShowSpeed);
    }

    static void Apply(Text label, Image pill, bool on)
    {
        if (label != null) label.text = on ? "LIGADO" : "DESLIGADO";
        if (pill != null) pill.color = on ? OnColor : OffColor;
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
