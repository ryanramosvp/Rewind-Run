using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cronômetro, contador de mortes e medidor de velocidade durante a fase.
///
/// Só lê e mostra: o tempo e as mortes são do <see cref="GameSession"/>, e
/// nada aqui conta, para ou zera coisa alguma.
///
/// O cronômetro muda de cor conforme o rank que o tempo atual ainda alcança.
/// Isso importa numa corrida: saber "estou perdendo o S" enquanto ainda dá
/// para correr atrás é informação útil; descobrir no fim, não.
///
/// Os dígitos vão em largura fixa. Com a fonte proporcional, o "1" é mais
/// estreito que o "0" e o número inteiro tremia de lado a cada centésimo.
///
/// Não há mais contador de corrente. O bunny hop é mecânica escondida: quem
/// descobre que pulos encadeados aceleram, descobre pelo jogo ficando mais
/// rápido, não por um "x3" aparecendo no canto. Um número na tela transforma
/// uma descoberta em instrução.
///
/// A barra de velocidade sobrou, mas desligada por padrão. Ela é o resto do
/// sistema que ainda denuncia o embalo, então quem quiser ver pede nas opções.
/// </summary>
public class SpeedrunHud : MonoBehaviour
{
    [SerializeField] GameObject timerGroup;
    [SerializeField] TMP_Text timerLabel;

    [Tooltip("Placa das mortes. Some junto com o placar quando a cena não tem sessão (tutorial).")]
    [SerializeField] GameObject deathsGroup;
    [SerializeField] TMP_Text deathsLabel;
    [SerializeField] UiPulse deathsPulse;

    [SerializeField] GameObject speedGroup;
    [SerializeField] Image speedFill;

    [SerializeField] PlayerController player;

    const string kDigits = "<mspace=0.6em>{0}</mspace>";

    int _shownDeaths = -1;

    void OnEnable()
    {
        GameSettings.Changed += ApplyVisibility;
        ApplyVisibility();
    }

    void OnDisable() => GameSettings.Changed -= ApplyVisibility;

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        ApplyVisibility();
    }

    void ApplyVisibility()
    {
        bool hasSession = GameSession.Instance != null;

        if (timerGroup != null) timerGroup.SetActive(hasSession && GameSettings.ShowTimer);
        if (deathsGroup != null) deathsGroup.SetActive(hasSession);
        if (speedGroup != null) speedGroup.SetActive(GameSettings.ShowSpeed);
    }

    void Update()
    {
        var session = GameSession.Instance;

        if (session != null)
        {
            if (timerLabel != null && GameSettings.ShowTimer)
            {
                float elapsed = session.Elapsed;

                timerLabel.text = string.Format(kDigits, GameSession.FormatTime(elapsed));
                timerLabel.color = Ranks.ColorOf(session.RankFor(elapsed));
            }

            if (deathsLabel != null && session.Deaths != _shownDeaths)
            {
                // Pulsa só quando sobe, não no primeiro quadro da fase.
                if (_shownDeaths >= 0 && session.Deaths > _shownDeaths && deathsPulse != null) deathsPulse.Play();

                _shownDeaths = session.Deaths;
                deathsLabel.text = string.Format(kDigits, _shownDeaths);
            }
        }

        if (player == null || !GameSettings.ShowSpeed) return;

        if (speedFill != null)
        {
            float speed = player.HorizontalVelocity.magnitude;
            speedFill.fillAmount = Mathf.Clamp01(speed / Mathf.Max(1f, player.TopSpeed));
        }
    }
}
