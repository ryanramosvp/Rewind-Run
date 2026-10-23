using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cronômetro e medidor de velocidade durante a fase.
///
/// O cronômetro muda de cor conforme o rank que o tempo atual ainda alcança.
/// Isso importa numa corrida: saber "estou perdendo o S" enquanto ainda dá
/// para correr atrás é informação útil; descobrir no fim, não.
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
    [SerializeField] Text timerLabel;

    [SerializeField] GameObject speedGroup;
    [SerializeField] Image speedFill;

    [SerializeField] PlayerController player;

    void OnEnable()
    {
        GameSettings.Changed += ApplyVisibility;
        ApplyVisibility();
    }

    void OnDisable() => GameSettings.Changed -= ApplyVisibility;

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
    }

    void ApplyVisibility()
    {
        if (timerGroup != null) timerGroup.SetActive(GameSettings.ShowTimer);
        if (speedGroup != null) speedGroup.SetActive(GameSettings.ShowSpeed);
    }

    void Update()
    {
        var session = GameSession.Instance;

        if (session != null && timerLabel != null && GameSettings.ShowTimer)
        {
            float elapsed = session.Elapsed;

            timerLabel.text = GameSession.FormatTime(elapsed);
            timerLabel.color = Ranks.ColorOf(session.RankFor(elapsed));
        }

        if (player == null || !GameSettings.ShowSpeed) return;

        if (speedFill != null)
        {
            float speed = player.HorizontalVelocity.magnitude;
            speedFill.fillAmount = Mathf.Clamp01(speed / Mathf.Max(1f, player.TopSpeed));
        }
    }
}
