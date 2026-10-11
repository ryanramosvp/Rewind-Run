using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Telas de morte e de vitória.
///
/// A de morte sai com qualquer tecla, de propósito. Num platformer de precisão
/// morre-se dezenas de vezes, e uma tela que exige navegar um menu a cada queda
/// transforma o erro em punição administrativa. Assim ela ainda é uma tela -
/// mostra o placar e as saídas - mas some num toque.
/// </summary>
public class EndScreen : MonoBehaviour
{
    [SerializeField] GameObject deathPanel;
    [SerializeField] GameObject victoryPanel;
    [SerializeField] Text deathSummary;
    [SerializeField] Text victorySummary;
    [SerializeField] Text medalLabel;
    [SerializeField] Text timeValue;
    [SerializeField] Text fallsValue;
    [SerializeField] Text bestValue;

    [Tooltip("Cena do menu inicial, carregada com Esc.")]
    [SerializeField] string menuScene = "Menu";

    [Tooltip("Fase seguinte. Vazio significa que esta é a última, e aí Espaço " +
             "repete a corrida em vez de avançar.")]
    [SerializeField] string nextLevelScene;

    bool _showing;
    bool _isVictory;

    /// <summary>Alguma tela de fim está no ar? A pausa consulta antes de abrir.</summary>
    public bool IsShowing => _showing;

    void Awake() => HideAll();

    void OnEnable()
    {
        var respawn = LevelRespawn.Instance;
        if (respawn != null) respawn.Died += ShowDeath;
    }

    void OnDisable()
    {
        var respawn = LevelRespawn.Instance;
        if (respawn != null) respawn.Died -= ShowDeath;
    }

    void Update()
    {
        if (!_showing) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuScene);
            return;
        }

        if (_isVictory)
        {
            // R sempre repete, mesmo havendo fase seguinte: num jogo de tempo,
            // repetir a fase que se acabou de vencer é o gesto mais comum que
            // existe, e não pode depender de voltar ao menu para achá-lo.
            if (keyboard.rKey.wasPressedThisFrame)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(string.IsNullOrEmpty(nextLevelScene)
                                           ? SceneManager.GetActiveScene().name
                                           : nextLevelScene);
            }

            return;
        }

        if (keyboard.anyKey.wasPressedThisFrame) Resume();
    }

    /// <summary>Chamada pelo <see cref="LevelRespawn"/> a cada reinício por morte.</summary>
    public void ShowDeath()
    {
        var session = GameSession.Instance;
        if (session != null) session.RegisterDeath();

        // Já venceu: uma morte tardia não deve roubar a tela de vitória.
        if (_isVictory) return;

        if (deathSummary != null && session != null)
        {
            // "queda" descrevia só um dos jeitos de morrer. Com prensa e
            // hélice na fase, o texto tem que valer para todos.
            deathSummary.text = $"morte nº {session.Deaths}\n\n" +
                                "qualquer tecla para continuar\nEsc para o menu";
        }

        Show(deathPanel, victory: false);
    }

    public void ShowVictory()
    {
        var session = GameSession.Instance;
        if (session != null)
        {
            session.Finish();
            WriteVictorySummary(session);
        }

        Show(victoryPanel, victory: true);
    }

    /// <summary>
    /// Monta o resumo da corrida.
    ///
    /// A medalha vem primeiro e grande porque e o veredito; tempo e recorde sao
    /// a justificativa. Quando o recorde cai, ele e dito explicitamente - um
    /// numero menor que o anterior passa despercebido se ninguem apontar.
    /// </summary>
    void WriteVictorySummary(GameSession session)
    {
        var medal = session.EarnedMedal;

        if (medalLabel != null)
        {
            medalLabel.text = medal == Medal.Nenhuma ? "SEM MEDALHA" : medal.ToString().ToUpperInvariant();
            medalLabel.color = GameSession.ColorOf(medal);
        }

        if (timeValue != null) timeValue.text = GameSession.FormatTime(session.Elapsed);
        if (fallsValue != null) fallsValue.text = session.Deaths.ToString();

        if (bestValue != null)
        {
            // Recorde novo e dito em palavra, nao so em numero menor: um numero
            // que caiu passa despercebido se ninguem apontar.
            bestValue.text = session.IsNewRecord
                ? "NOVO"
                : session.PreviousBest > 0f ? GameSession.FormatTime(session.PreviousBest) : "--";
        }

        if (victorySummary != null)
            // Separador visivel, nao uma fileira de espacos: agora que o texto
            // quebra linha, uma sequencia de espacos vira um ponto de quebra
            // qualquer e o alinhamento se desfaz.
            victorySummary.text = string.IsNullOrEmpty(nextLevelScene)
                ? "Espaco ou R para correr de novo   |   Esc para o menu"
                : "Espaco: proxima fase   |   R: correr de novo   |   Esc: menu";
    }

    void Show(GameObject panel, bool victory)
    {
        HideAll();

        if (panel != null) panel.SetActive(true);

        _showing = true;
        _isVictory = victory;

        if (victory) UiSounds.Victory();

        // Congela o jogo em vez de deixá-lo rodando atrás: o respawn já
        // aconteceu, e ver a bruxa andando sob a tela de morte confundiria.
        Time.timeScale = 0f;

        OrbitCamera.FreeCursor();
    }

    void Resume()
    {
        HideAll();
        _showing = false;

        Time.timeScale = 1f;

        // Pergunta à câmera em vez de prender. Prendia sempre, e como esta fase
        // usa câmera fixa, o cursor sumia depois da primeira morte - e com ele
        // preso o Editor do Unity passa a engolir o Esc para soltá-lo, o que
        // deixava a pausa sem a própria tecla.
        OrbitCamera.RestoreCursor();
    }

    void HideAll()
    {
        if (deathPanel != null) deathPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }
}
