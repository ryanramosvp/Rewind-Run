using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField] TMP_Text timerToggleLabel;
    [SerializeField] TMP_Text speedToggleLabel;
    [SerializeField] Image timerPill;
    [SerializeField] Image speedPill;
    [SerializeField] Slider volumeSlider;
    [SerializeField] TMP_Text volumeLabel;

    [Header("Campanha")]
    [Tooltip("Desligado enquanto não há campanha em andamento.")]
    [SerializeField] Button continueButton;
    [Tooltip("Nome da fase que o Continuar abre, embaixo do botão.")]
    [SerializeField] TMP_Text continueDetail;
    [SerializeField] Button newGameButton;

    [Header("Seleção de fases, na ordem da Campaign")]
    [SerializeField] GameObject levelSelectPanel;
    [SerializeField] Button[] levelButtons;
    [SerializeField] TMP_Text[] levelTitles;
    [SerializeField] TMP_Text[] levelDetails;
    [Tooltip("Letra da nota do recorde, na cor do rank.")]
    [SerializeField] TMP_Text[] levelRanks;
    [SerializeField] Button levelSelectBackButton;

    [Header("Confirmação")]
    [SerializeField] GameObject confirmPanel;
    [SerializeField] TMP_Text confirmTitle;
    [SerializeField] TMP_Text confirmMessage;
    [Tooltip("Recebe o foco ao abrir: Enter sem ler não pode apagar nada.")]
    [SerializeField] Button confirmCancelButton;

    static readonly Color OnColor = new Color(0f, 0.518f, 0.329f);
    static readonly Color OffColor = new Color(0.29f, 0.32f, 0.33f);

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
        var gamepad = Gamepad.current;

        // Esc sempre fecha, mesmo que o botão não esteja visível por qualquer
        // motivo. É a rede de segurança contra ficar preso numa tela. Na
        // confirmação ele vale como Cancelar. No controle, o B faz o mesmo.
        bool back = (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame
                                          || keyboard.backspaceKey.wasPressedThisFrame))
                    || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);

        if (back)
        {
            if (confirming) CancelConfirm();
            else if (controlsPanel != null && controlsPanel.activeSelf) OpenSettings();
            else CloseControls();
        }
    }

    /// <summary>
    /// Sempre há uma opção escolhida. Se a seleção se perde - um clique no
    /// fundo, um botão que sumiu, o primeiro quadro em que o EventSystem ainda
    /// não aceitou a seleção do Start -, ela volta para a opção padrão da tela
    /// aberta. Sem isso o teclado e o controle ficavam sem ter o que navegar,
    /// e nada aparecia aceso.
    /// </summary>
    void LateUpdate()
    {
        var system = EventSystem.current;
        if (system == null) return;

        var current = system.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy) return;

        var fallback = DefaultSelection();
        if (fallback != null) fallback.Select();
    }

    /// <summary>A opção que recebe o foco em cada tela.</summary>
    Selectable DefaultSelection()
    {
        if (confirmPanel != null && confirmPanel.activeSelf) return confirmCancelButton;
        if (tutorialPanel != null && tutorialPanel.activeSelf) return tutorialYesButton;
        if (controlsPanel != null && controlsPanel.activeSelf) return backButton;
        if (settingsPanel != null && settingsPanel.activeSelf) return settingsBackButton;

        if (levelSelectPanel != null && levelSelectPanel.activeSelf)
        {
            int current = Campaign.IndexOf(SaveSystem.CurrentLevel);
            return TryGet(levelButtons, current, out var level) && level.interactable ? level : levelSelectBackButton;
        }

        if (continueButton != null && continueButton.interactable) return continueButton;
        return newGameButton != null ? newGameButton : firstButton;
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
    /// Escreve cada linha da seleção: nome, recorde, mortes da corrida do
    /// recorde e a nota dele. Bloqueada fica apagada (o botão desligado cuida
    /// disso), com o aviso no lugar do recorde, e não aceita clique.
    ///
    /// A nota vem da tabela de ranks que o save copia da fase ao jogá-la;
    /// fase nunca aberta desde então fica sem letra.
    /// </summary>
    void RefreshLevelSelect()
    {
        for (int i = 0; i < Campaign.Levels.Length; i++)
        {
            var level = Campaign.Levels[i];
            bool unlocked = SaveSystem.IsUnlocked(level.Id);
            var record = SaveSystem.Get(level.Id);

            if (TryGet(levelButtons, i, out var button)) button.interactable = unlocked;
            if (TryGet(levelTitles, i, out var title)) title.text = $"{i + 1}. {level.Title.ToUpper()}";

            if (TryGet(levelRanks, i, out var letter))
            {
                var rank = unlocked ? record.RankOf(record.bestTime) : null;
                letter.text = rank.HasValue ? rank.Value.ToString() : "";
                if (rank.HasValue) letter.color = Ranks.ColorOf(rank.Value);
            }

            if (!TryGet(levelDetails, i, out var detail)) continue;

            if (!unlocked)
            {
                detail.text = "BLOQUEADA";
            }
            else if (record.bestTime > 0f)
            {
                string deaths = record.bestRunDeaths == 1 ? "1 morte" : $"{record.bestRunDeaths} mortes";
                detail.text = $"RECORDE {GameSession.FormatTime(record.bestTime)}  ·  {deaths}";
            }
            else
            {
                detail.text = "SEM RECORDE AINDA";
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

    /// <summary>Aberta de dentro das configurações; o Voltar dela volta para lá.</summary>
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
        var focus = DefaultSelection();
        if (focus != null) focus.Select();
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

    static void Apply(TMP_Text label, Image pill, bool on)
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
