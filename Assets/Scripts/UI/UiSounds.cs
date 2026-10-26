using UnityEngine;

/// <summary>
/// Os sons de interface da cena, num lugar só.
///
/// É estático porque quem toca um som - um botão, a tela de vitória - não tem
/// como ter uma referência para o tocador sem que alguém a ligue à mão em cada
/// um. Com dezenas de botões gerados por código, uma referência esquecida seria
/// um botão mudo que ninguém nota até a gravação do vídeo.
///
/// Toca por <c>PlayOneShot</c> num AudioSource só: sons curtos de interface se
/// sobrepõem o tempo todo (passar por três opções depressa), e um Play comum
/// cortaria o anterior a cada vez.
///
/// Nada aqui depende do timeScale, então a pausa continua com som. Unity só
/// silencia o jogo quando AudioListener.pause é ligado, o que não fazemos.
/// </summary>
public class UiSounds : MonoBehaviour
{
    public static UiSounds Instance { get; private set; }

    [Tooltip("Navegar entre opções: passar o mouse ou mover a seleção.")]
    [SerializeField] AudioClip select;

    [Tooltip("Apertar uma opção.")]
    [SerializeField] AudioClip confirm;

    [Tooltip("Fanfarra de fim de fase.")]
    [SerializeField] AudioClip victory;

    [SerializeField] AudioSource source;

    [SerializeField, Range(0f, 1f)] float uiVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] float victoryVolume = 0.7f;

    void Awake()
    {
        Instance = this;
        if (source == null) source = GetComponent<AudioSource>();

        // Toda cena com interface passa por aqui, então é o lugar certo para
        // restaurar o volume salvo.
        GameSettings.ApplyVolume();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Select() => Play(Instance?.select, Instance != null ? Instance.uiVolume : 0f);

    public static void Confirm() => Play(Instance?.confirm, Instance != null ? Instance.uiVolume : 0f);

    /// <summary>Fanfarra de vitória, com a música de fundo calada enquanto ela toca.</summary>
    public static void Victory()
    {
        var clip = Instance != null ? Instance.victory : null;
        if (clip != null) MusicPlayer.Hush(clip.length);
        Play(clip, Instance != null ? Instance.victoryVolume : 0f);
    }

    static void Play(AudioClip clip, float volume)
    {
        if (clip == null || Instance == null || Instance.source == null) return;

        Instance.source.PlayOneShot(clip, volume);
    }
}
