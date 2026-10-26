using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Botões do menu inicial.
///
/// O painel de controles esconde os botões em vez de abrir por cima deles.
/// Sobrepostos, o painel cobria o próprio botão que o fecharia, e sem tecla de
/// saída o menu virava um beco sem saída. As telas de fases e de confirmação
/// seguem a mesma regra.
///
/// A campanha mora no <see cref="SaveSystem"/>: o menu só lê o que está lá e
/// pede as mudanças. Continuar abre a fase atual do save, sempre do começo;
/// Novo jogo zera a campanha e mantém os recordes; apagar os dados (nas
/// opções) leva tudo. As duas últimas pedem confirmação.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Primeira fase, carregada quando o Novo jogo pula o tutorial.")]
    [SerializeField] string levelScene = "Level_CorridaDeLava";

    [Tooltip("Cena do tutorial. Ao terminar, ela mesma segue para a fase.")]
    [SerializeField] string tutorialScene = "Tutorial";

    [Tooltip("Pergunta se quer fazer o tutorial. Vazio faz o Novo jogo ir direto para a fase.")]
    [SerializeField] GameObject tutorialPanel;
    [SerializeField] Button tutorialYesButton;

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

    [Header("Campanha")]
    [Tooltip("Desligado enquanto não há campanha em andamento.")]
    [SerializeField] Button continueButton;
    [Tooltip("Nome da fase que o Continuar abre, embaixo do botão.")]
    [SerializeField] Text continueDetail;
    [SerializeField] Button newGameButton;

    [Header("Seleção de fases, na ordem da Campaign")]
    [SerializeField] GameObject levelSelectPanel;
    [SerializeField] Button[] levelButtons;
    [SerializeField] Text[] levelTitles;
    [SerializeField] Text[] levelDetails;
    [SerializeField] Button levelSelectBackButton;

    [Header("Confirmação")]
    [SerializeField] GameObject confirmPanel;
    [SerializeField] Text confirmTitle;
    [SerializeField] Text confirmMessage;
    [Tooltip("Recebe o foco ao abrir: Enter sem ler não pode apagar nada.")]
    [SerializeField] Button confirmCancelButton;

    static readonly Color OnColor = new Color(0f, 0.518f, 0.329f);
    static readonly Color OffColor = new Color(0.29f, 0.32f, 0.33f);
    static readonly Color LockedColor = new Color(0.45f, 0.47f, 0.48f);
    static readonly Color Sand = new Color(0.855f, 0.682f, 0.490f);
    static readonly Color Ash = new Color(0.831f, 0.859f, 0.871f);

    enum Pending { None, NewGame, DeleteAll }

    Pending _pending;
    GameObject _confirmReturn;

    void Start()
    {
        // O menu é o único lugar do jogo com cursor; em jogo ele fica preso.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        // Lê o save (ou cria, na primeira vez) antes de decidir o que o menu
        // oferece.
        SaveSystem.Initialize();

        CloseControls();
    }

    void Update()
    {
        bool confirming = confirmPanel != null && confirmPanel.activeSelf;
        bool open = confirming
                    || (controlsPanel != null && controlsPanel.activeSelf)
                    || (settingsPanel != null && settingsPanel.activeSelf)
                    || (tutorialPanel != null && tutorialPanel.activeSelf)
                    || (levelSelectPanel != null && levelSelectPanel.activeSelf);

        if (!open) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Esc sempre fecha, mesmo que o botão não esteja visível por qualquer
        // motivo. É a rede de segurança contra ficar preso numa tela. Na
        // confirmação ele vale como Cancelar.
        if (keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
        {
            if (confirming) CancelConfirm();
            else CloseControls();
        }
    }

    // ------------------------------------------------------------------
    // Campanha
    // ------------------------------------------------------------------

    /// <summary>Abre a fase atual do save, do começo.</summary>
    public void Continue()
    {
        if (!SaveSystem.HasProgress) return;
        SceneManager.LoadScene(SaveSystem.CurrentLevel);
    }

    /// <summary>
    /// Recomeça a campanha. Sem nada em andamento não há o que perder, e vai
    /// direto; com progresso, pergunta antes.
    /// </summary>
    public void NewGame()
    {
        if (!SaveSystem.HasProgress)
        {
            StartNewGame();
            return;
        }

        AskConfirmation(Pending.NewGame, "NOVO JOGO?",
                        "A campanha volta para a primeira fase e as outras são bloqueadas de novo. " +
                        "Seus recordes de tempo continuam guardados.",
                        null);
    }

    /// <summary>Fica nas opções: é a única ação do menu que não se desfaz.</summary>
    public void AskDeleteAll()
    {
        AskConfirmation(Pending.DeleteAll, "APAGAR TODOS OS DADOS?",
                        "Apaga a campanha e os recordes de todas as fases. Não dá para desfazer.",
                        settingsPanel);
    }

    public void Confirm()
    {
        var action = _pending;
        _pending = Pending.None;

        switch (action)
        {
            case Pending.NewGame:
                StartNewGame();
                break;

            case Pending.DeleteAll:
                SaveSystem.DeleteAll();
                ReturnFromConfirm();
                break;

            default:
                ReturnFromConfirm();
                break;
        }
    }

    public void CancelConfirm()
    {
        _pending = Pending.None;
        ReturnFromConfirm();
    }

    /// <summary>
    /// Zera a campanha e segue para a pergunta do tutorial, como o antigo
    /// Jogar. O save já fica zerado aqui: a confirmação foi dada.
    /// </summary>
    void StartNewGame()
    {
        SaveSystem.NewGame();
        RefreshCampaign();
        Play();
    }

    void AskConfirmation(Pending action, string title, string message, GameObject returnTo)
    {
        _pending = action;
        _confirmReturn = returnTo;

        HidePanels();
        if (buttonsGroup != null) buttonsGroup.SetActive(false);

        if (confirmTitle != null) confirmTitle.text = title;
        if (confirmMessage != null) confirmMessage.text = message;
        if (confirmPanel != null) confirmPanel.SetActive(true);

        if (confirmCancelButton != null) confirmCancelButton.Select();
    }

    /// <summary>Volta para a tela de onde a confirmação foi aberta.</summary>
    void ReturnFromConfirm()
    {
        if (_confirmReturn != null && _confirmReturn == settingsPanel) OpenSettings();
        else CloseControls();
    }

    public void OpenLevelSelect()
    {
        HidePanels();
        if (buttonsGroup != null) buttonsGroup.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);

        RefreshLevelSelect();

        // O foco vai para a fase atual, a mais provável de ser escolhida.
        int current = Campaign.IndexOf(SaveSystem.CurrentLevel);
        if (TryGet(levelButtons, current, out var button) && button.interactable) button.Select();
        else if (levelSelectBackButton != null) levelSelectBackButton.Select();
    }

    /// <summary>
    /// Joga uma fase pela posição na campanha. Bloqueada não abre, mesmo que o
    /// método seja chamado por fora da interface.
    /// </summary>
    public void PlayLevel(int index)
    {
        if (index < 0 || index >= Campaign.Levels.Length) return;

        string id = Campaign.Levels[index].Id;
        if (!SaveSystem.IsUnlocked(id)) return;

        SceneManager.LoadScene(id);
    }

    void RefreshCampaign()
    {
        bool hasProgress = SaveSystem.HasProgress;

        if (continueButton != null) continueButton.interactable = hasProgress;

        if (continueDetail != null)
        {
            int index = Campaign.IndexOf(SaveSystem.CurrentLevel);
            continueDetail.text = hasProgress && index >= 0 ? Campaign.Levels[index].Title.ToUpper() : "";
        }
    }

    /// <summary>
    /// Escreve cada linha da seleção: nome, recorde e mortes da corrida do
    /// recorde. Bloqueada fica apagada, com o aviso no lugar do recorde, e não
    /// aceita clique.
    ///
    /// Sem rank: a tabela de ranks de cada fase mora na cena dela, e o menu
    /// não a tem. O save guarda o tempo, que é o que não muda.
    /// </summary>
    void RefreshLevelSelect()
    {
        for (int i = 0; i < Campaign.Levels.Length; i++)
        {
            var level = Campaign.Levels[i];
            bool unlocked = SaveSystem.IsUnlocked(level.Id);

            if (TryGet(levelButtons, i, out var button)) button.interactable = unlocked;

            if (TryGet(levelTitles, i, out var title))
            {
                title.text = $"{i + 1}. {level.Title.ToUpper()}";
                title.color = unlocked ? Sand : LockedColor;
            }

            if (!TryGet(levelDetails, i, out var detail)) continue;

            if (!unlocked)
            {
                detail.text = "BLOQUEADA";
                detail.color = LockedColor;
                continue;
            }

            var record = SaveSystem.Get(level.Id);
            detail.color = Ash;

            if (record.bestTime > 0f)
            {
                string deaths = record.bestRunDeaths == 1 ? "1 morte" : $"{record.bestRunDeaths} mortes";
                detail.text = $"Recorde {GameSession.FormatTime(record.bestTime)}  ·  {deaths}";
            }
            else
            {
                detail.text = "Sem recorde ainda";
            }
        }
    }

    static bool TryGet<T>(T[] items, int index, out T item) where T : Object
    {
        item = items != null && index >= 0 && index < items.Length ? items[index] : null;
        return item != null;
    }

    void HidePanels()
    {
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Tutorial, controles e opções
    // ------------------------------------------------------------------

    /// <summary>
    /// Pergunta toda vez, sem lembrar a resposta. Quem joga a entrega costuma
    /// ser uma pessoa nova a cada partida no mesmo computador, e uma escolha
    /// gravada pelo jogador anterior esconderia o tutorial de quem precisa dele.
    /// </summary>
    public void Play()
    {
        if (tutorialPanel == null)
        {
            SkipTutorial();
            return;
        }

        HidePanels();
        tutorialPanel.SetActive(true);
        if (buttonsGroup != null) buttonsGroup.SetActive(false);

        if (tutorialYesButton != null) tutorialYesButton.Select();
    }

    public void PlayTutorial() => SceneManager.LoadScene(tutorialScene);

    public void SkipTutorial() => SceneManager.LoadScene(levelScene);

    public void OpenControls()
    {
        HidePanels();
        if (controlsPanel != null) controlsPanel.SetActive(true);
        if (buttonsGroup != null) buttonsGroup.SetActive(false);

        // Deixa um botão selecionado para teclado e controle funcionarem sem
        // precisar clicar primeiro.
        if (backButton != null) backButton.Select();
    }

    public void CloseControls()
    {
        HidePanels();
        if (buttonsGroup != null) buttonsGroup.SetActive(true);

        RefreshCampaign();

        // Com campanha em andamento o foco vai para o Continuar; sem ela, ele
        // está desligado e o foco cai no Novo jogo.
        if (continueButton != null && continueButton.interactable) continueButton.Select();
        else if (newGameButton != null) newGameButton.Select();
        else if (firstButton != null) firstButton.Select();
    }

    public void OpenSettings()
    {
        HidePanels();
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
