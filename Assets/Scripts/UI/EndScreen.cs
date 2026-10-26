using TMPro;
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
    [SerializeField] TMP_Text deathSummary;
    [SerializeField] TMP_Text victorySummary;
    [SerializeField] TMP_Text rankLabel;
    [SerializeField] TMP_Text timeValue;
    [SerializeField] TMP_Text fallsValue;
    [SerializeField] TMP_Text bestValue;

    [Header("Escada de ranks, de S a F")]
    [SerializeField] Image[] ladderPanels;
    [SerializeField] TMP_Text[] ladderLetters;
    [SerializeField] TMP_Text[] ladderLimits;
    [SerializeField] TMP_Text[] ladderDeltas;

    [SerializeField] TMP_Text nextRankLabel;
    [SerializeField] TMP_Text recordLabel;

    [Tooltip("Cena do menu inicial, carregada com Esc.")]
    [SerializeField] string menuScene = "Menu";

    [Tooltip("Fase seguinte. Vazio significa que esta é a última, e aí Espaço " +
             "repete a corrida em vez de avançar.")]
    [SerializeField] string nextLevelScene;

    [Tooltip("Como a dica chama o que vem depois: próxima fase, ou ver resultados na última.")]
    [SerializeField] string nextLevelLabel = "próxima fase";

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
        var pad = Gamepad.current;

        // Teclado e controle lado a lado. Na vitória, Esc ou B vão para o
        // menu; na morte o B continua, como qualquer outro botão, e o menu
        // fica no Select - o B é o "voltar" do controle, e apertá-lo por
        // reflexo na tela de morte não pode tirar ninguém da fase.
        bool toMenu = Pressed(keyboard?.escapeKey)
                      || (_isVictory ? Pressed(pad?.buttonEast) : Pressed(pad?.selectButton));
        bool next = Pressed(keyboard?.spaceKey) || Pressed(keyboard?.enterKey) || Pressed(pad?.buttonSouth);
        bool retry = Pressed(keyboard?.rKey) || Pressed(pad?.buttonWest);

        if (toMenu)
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
            if (retry)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }

            if (next)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(string.IsNullOrEmpty(nextLevelScene)
                                           ? SceneManager.GetActiveScene().name
                                           : nextLevelScene);
            }

            return;
        }

        bool anyPad = pad != null && (Pressed(pad.buttonSouth) || Pressed(pad.buttonEast)
                                      || Pressed(pad.buttonWest) || Pressed(pad.buttonNorth)
                                      || Pressed(pad.startButton));
        if ((keyboard != null && keyboard.anyKey.wasPressedThisFrame) || anyPad) Resume();
    }

    static bool Pressed(UnityEngine.InputSystem.Controls.ButtonControl button) =>
        button != null && button.wasPressedThisFrame;

    /// <summary>Chamada pelo <see cref="LevelRespawn"/> a cada reinício por morte.</summary>
    public void ShowDeath()
    {
        var session = GameSession.Instance;
        if (session != null) session.RegisterDeath();

        // Já venceu: uma morte tardia não deve roubar a tela de vitória.
        if (_isVictory) return;

        if (deathSummary != null)
        {
            // "queda" descrevia só um dos jeitos de morrer. Com prensa e
            // hélice na fase, o texto tem que valer para todos. Sem placar -
            // o tutorial não tem - fica só o como sair, que é o que importa.
            string count = session != null ? $"morte nº {session.Deaths}\n\n" : string.Empty;
            deathSummary.text = count + "qualquer tecla ou botão para continuar\nEsc ou Select para o menu";
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
    /// O rank vem primeiro e grande porque é o veredito; tempo e recorde são a
    /// justificativa. Quando o recorde cai, ele é dito explicitamente - um
    /// número menor que o anterior passa despercebido se ninguém apontar.
    /// </summary>
    void WriteVictorySummary(GameSession session)
    {
        var rank = session.EarnedRank;

        if (rankLabel != null)
        {
            rankLabel.text = rank.ToString();
            rankLabel.color = Ranks.ColorOf(rank);
        }

        WriteLadder(session, rank);
        WriteNextRank(session, rank);
        WriteRecord(session);

        if (timeValue != null) timeValue.text = GameSession.FormatTime(session.Elapsed);
        if (fallsValue != null) fallsValue.text = session.Deaths.ToString();

        if (bestValue != null)
        {
            // Sempre o tempo anterior, que é o que o rótulo promete. O recorde
            // novo é anunciado na linha de baixo, com a diferença.
            bestValue.text = session.PreviousBest > 0f ? GameSession.FormatTime(session.PreviousBest) : "--";
        }

        if (victorySummary != null)
            // Separador visivel, nao uma fileira de espacos: agora que o texto
            // quebra linha, uma sequencia de espacos vira um ponto de quebra
            // qualquer e o alinhamento se desfaz.
            victorySummary.text = string.IsNullOrEmpty(nextLevelScene)
                ? "ESPAÇO / A  correr de novo      ESC / B  menu"
                : $"ESPAÇO / A  {nextLevelLabel}      R / X  correr de novo      ESC / B  menu";
    }

    /// <summary>
    /// A escada inteira, e não só o rank alcançado.
    ///
    /// Cada degrau mostra o limite e a distância do tempo feito até ele. Com a
    /// escada toda à vista, quem tirou B vê no mesmo relance que o A estava a
    /// 1,8 s e o S a 6 s - e é isso que faz correr de novo, não a letra.
    /// </summary>
    void WriteLadder(GameSession session, Rank earned)
    {
        float time = session.Elapsed;
        var dim = new Color(0.16f, 0.18f, 0.19f, 0.95f);
        var ink = new Color(0.075f, 0.098f, 0.106f);
        var ash = new Color(0.831f, 0.859f, 0.871f);

        for (int i = 0; i <= (int)Rank.F; i++)
        {
            var rank = (Rank)i;
            bool isTimed = i < Ranks.Timed;
            float limit = session.LimitOf(rank);

            // O degrau alcançado fica com a cor cheia do rank e texto escuro,
            // como um botão selecionado; os outros ficam apagados. Com a cor
            // só translúcida, a letra B azul sumia sobre o próprio fundo azul.
            bool lit = rank == earned;

            if (TryGet(ladderPanels, i, out var panel))
                panel.color = lit ? Ranks.ColorOf(rank) : dim;

            if (TryGet(ladderLetters, i, out var letter))
                letter.color = lit ? ink : Ranks.ColorOf(rank);

            if (TryGet(ladderLimits, i, out var limitLabel))
            {
                limitLabel.text = isTimed
                    ? "até " + GameSession.FormatTime(limit)
                    : "acima de " + GameSession.FormatTime(limit);
                limitLabel.color = lit ? ink : ash;
            }

            if (!TryGet(ladderDeltas, i, out var deltaLabel)) continue;

            if (!isTimed)
            {
                deltaLabel.text = lit ? "aqui" : "--";
                deltaLabel.color = lit ? ink : new Color(0.6f, 0.62f, 0.64f);
                continue;
            }

            float delta = time - limit;
            deltaLabel.text = GameSession.FormatDelta(delta);
            deltaLabel.color = lit ? ink
                             : delta <= 0f ? new Color(0.45f, 0.88f, 0.55f)
                             : new Color(1f, 0.45f, 0.4f);
        }
    }

    static bool TryGet<T>(T[] items, int index, out T item) where T : Object
    {
        item = items != null && index < items.Length ? items[index] : null;
        return item != null;
    }

    /// <summary>
    /// Quanto faltou para o rank de cima, em palavras - e, quando o recorde
    /// cai, por quanto. Recorde novo é dito em palavra, não só em número
    /// menor: um número que caiu passa despercebido se ninguém apontar.
    /// </summary>
    void WriteNextRank(GameSession session, Rank earned)
    {
        if (nextRankLabel == null) return;

        string goal;
        if (earned == Rank.S)
        {
            goal = "Rank máximo. Agora é contra o próprio recorde.";
        }
        else
        {
            var next = (Rank)((int)earned - 1);
            float missing = session.Elapsed - session.LimitOf(next);

            goal = $"Próximo rank: {next} em {GameSession.FormatTime(session.LimitOf(next))}" +
                   $"  ·  faltaram {Seconds(missing)} s";
        }

        if (session.IsNewRecord)
        {
            string headline = session.PreviousBest > 0f
                ? $"NOVO RECORDE: {Seconds(session.PreviousBest - session.Elapsed)} s mais rápido"
                : "PRIMEIRO RECORDE NESTA FASE";
            goal = headline + "\n" + goal;
        }

        nextRankLabel.text = goal;
    }

    static string Seconds(float value) => value.ToString("0.00").Replace('.', ',');

    /// <summary>
    /// O que o save guarda desta fase: melhor tempo, quantas vezes foi
    /// concluída e as últimas corridas, para ver a evolução de uma vez.
    /// </summary>
    void WriteRecord(GameSession session)
    {
        if (recordLabel == null) return;

        var record = session.Record;
        var line = new System.Text.StringBuilder();

        line.Append($"Melhor: {GameSession.FormatTime(record.bestTime)} (rank {session.RankFor(record.bestTime)})");
        line.Append($"   ·   {record.completions} {(record.completions == 1 ? "corrida concluída" : "corridas concluídas")}");
        line.Append($"   ·   {record.totalDeaths} mortes no total");

        if (record.recentRuns.Count > 1)
        {
            line.Append("\nÚltimas: ");
            for (int i = 0; i < record.recentRuns.Count; i++)
            {
                if (i > 0) line.Append("   ");
                line.Append(GameSession.FormatTime(record.recentRuns[i].time));
            }
        }

        recordLabel.text = line.ToString();
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
