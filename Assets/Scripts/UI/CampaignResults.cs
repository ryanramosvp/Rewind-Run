using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tela de resultados da campanha, depois da última fase: tempo, nota e
/// mortes de cada fase, o total e uma nota final.
///
/// Mostra a corrida mais recente de cada fase - a desta campanha, para quem
/// veio direto da primeira à última - e o recorde ao lado, para comparar.
/// Tudo vem do <see cref="SaveSystem"/>; esta tela não conta nem grava nada.
///
/// A nota final é a média das notas das fases, arredondada para o rank mais
/// próximo. Uma fase sem nota (sem corrida ou sem tabela) fica fora da média,
/// em vez de puxá-la para F por um dado que só não existe.
/// </summary>
public class CampaignResults : MonoBehaviour
{
    [Header("Linhas, na ordem da Campaign")]
    [SerializeField] TMP_Text[] titles;
    [SerializeField] TMP_Text[] times;
    [SerializeField] TMP_Text[] ranks;
    [SerializeField] TMP_Text[] deaths;
    [SerializeField] TMP_Text[] bests;

    [Header("Total")]
    [SerializeField] TMP_Text totalTime;
    [SerializeField] TMP_Text totalDeaths;
    [SerializeField] TMP_Text finalRank;

    [Header("Saídas")]
    [SerializeField] Button playAgainButton;
    [SerializeField] Button menuButton;
    [SerializeField] string menuScene = "Menu";

    const string kDigits = "<mspace=0.6em>{0}</mspace>";

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        SaveSystem.Initialize();
        Fill();

        if (playAgainButton != null) playAgainButton.Select();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var pad = Gamepad.current;

        bool back = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                    || (pad != null && pad.buttonEast.wasPressedThisFrame);
        if (back) ToMenu();
    }

    /// <summary>Mantém sempre uma opção escolhida, como no menu principal.</summary>
    void LateUpdate()
    {
        var system = EventSystem.current;
        if (system == null) return;

        var current = system.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy) return;

        if (playAgainButton != null) playAgainButton.Select();
    }

    /// <summary>Recomeça a campanha pela primeira fase. Recordes e desbloqueios ficam.</summary>
    public void PlayAgain() => SceneManager.LoadScene(Campaign.FirstLevel);

    public void ToMenu() => SceneManager.LoadScene(menuScene);

    void Fill()
    {
        float total = 0f;
        int deathSum = 0;
        int rankSum = 0, ranked = 0;
        bool complete = true;

        for (int i = 0; i < Campaign.Levels.Length; i++)
        {
            var level = Campaign.Levels[i];
            var record = SaveSystem.Get(level.Id);
            var run = record.LastRun;

            Set(titles, i, $"{i + 1}. {level.Title.ToUpper()}");

            if (run == null)
            {
                complete = false;
                Set(times, i, "--:--,--");
                Set(ranks, i, "-");
                Set(deaths, i, "-");
                Set(bests, i, "");
                continue;
            }

            total += run.time;
            deathSum += run.deaths;

            Set(times, i, string.Format(kDigits, GameSession.FormatTime(run.time)));
            Set(deaths, i, run.deaths.ToString());
            Set(bests, i, record.bestTime > 0f ? "recorde " + GameSession.FormatTime(record.bestTime) : "");

            var rank = record.RankOf(run.time);
            if (TryGet(ranks, i, out var letter))
            {
                letter.text = rank.HasValue ? rank.Value.ToString() : "-";
                if (rank.HasValue) letter.color = Ranks.ColorOf(rank.Value);
            }

            if (rank.HasValue)
            {
                rankSum += (int)rank.Value;
                ranked++;
            }
        }

        if (totalTime != null)
            totalTime.text = complete ? string.Format(kDigits, GameSession.FormatTime(total)) : "--:--,--";
        if (totalDeaths != null) totalDeaths.text = deathSum.ToString();

        if (finalRank != null)
        {
            if (ranked == 0)
            {
                finalRank.text = "-";
            }
            else
            {
                var average = (Rank)Mathf.Clamp(Mathf.RoundToInt(rankSum / (float)ranked), 0, (int)Rank.F);
                finalRank.text = average.ToString();
                finalRank.color = Ranks.ColorOf(average);
            }
        }
    }

    static void Set(TMP_Text[] items, int index, string value)
    {
        if (TryGet(items, index, out var item)) item.text = value;
    }

    static bool TryGet(TMP_Text[] items, int index, out TMP_Text item)
    {
        item = items != null && index >= 0 && index < items.Length ? items[index] : null;
        return item != null;
    }
}
