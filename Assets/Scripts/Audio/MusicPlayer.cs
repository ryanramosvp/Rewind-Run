using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Música de fundo, uma só para o jogo inteiro.
///
/// Cada cena traz um objeto com este componente dizendo qual faixa quer. O
/// primeiro a nascer fica (DontDestroyOnLoad) e passa a ser o tocador; os das
/// cenas seguintes só entregam a faixa deles e somem. Assim a escolha da
/// música mora na cena - quem monta a fase decide - e a música não corta na
/// troca de cena: se a faixa pedida é a que já está tocando, ela segue de onde
/// estava. Repetir a fase depois de vencer não recomeça a música do zero.
/// Faixa diferente entra por cima da outra, numa troca curta.
///
/// Reage ao jogo sem mexer nele:
///  - pausa e telas de fim (timeScale zero) abaixam a música;
///  - no rewind ela desacelera e grava, como uma fita perdendo rotação, e
///    volta ao normal ao soltar. É o som da mecânica;
///  - a fanfarra de vitória pede silêncio (<see cref="Hush"/>): duas músicas
///    em tons diferentes ao mesmo tempo soam como erro, não como festa.
///
/// O volume geral continua no AudioListener (<see cref="GameSettings"/>);
/// aqui é só o nível da música em relação aos efeitos.
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    [Tooltip("Faixa desta cena. Vazio deixa tocando o que já estava.")]
    [SerializeField] AudioClip track;

    [Tooltip("Nível da música em relação aos efeitos sonoros.")]
    [SerializeField, Range(0f, 1f)] float musicVolume = 0.45f;

    [Tooltip("Quanto do volume sobra com o jogo parado (pausa, morte, vitória).")]
    [SerializeField, Range(0f, 1f)] float pausedLevel = 0.45f;

    [Tooltip("Rotação da fita durante o rewind: 1 é normal, menos é mais lento e grave.")]
    [SerializeField, Range(0.3f, 1f)] float rewindPitch = 0.72f;

    [SerializeField, Min(0.05f)] float crossfadeSeconds = 1.2f;

    static MusicPlayer s_Active;

    AudioSource _current;
    AudioSource _previous;
    float _fade = 1f;
    float _duck = 1f;
    float _pitch = 1f;
    float _hush = 1f;
    float _hushUntil;

    void Awake()
    {
        if (s_Active != null && s_Active != this)
        {
            // Já há um tocador vindo da cena anterior: entrega a faixa e sai.
            s_Active.Play(track);
            Destroy(gameObject);
            return;
        }

        s_Active = this;
        DontDestroyOnLoad(gameObject);

        _current = CreateSource();
        _previous = CreateSource();
        Play(track);
    }

    void OnDestroy()
    {
        if (s_Active == this) s_Active = null;
    }

    AudioSource CreateSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    /// <summary>
    /// Cala a música por alguns segundos, para outro som tocar sozinho - a
    /// fanfarra de fim de fase. Sai em um quarto de segundo e volta devagar
    /// depois, já no nível baixo da tela parada.
    /// </summary>
    public static void Hush(float seconds)
    {
        if (s_Active == null) return;
        s_Active._hushUntil = Mathf.Max(s_Active._hushUntil, Time.unscaledTime + seconds);
    }

    /// <summary>Troca para a faixa pedida. A mesma faixa segue sem recomeçar.</summary>
    public void Play(AudioClip clip)
    {
        if (clip == null || _current == null) return;
        if (_current.clip == clip && _current.isPlaying) return;

        // A que tocava vira a de saída; a nova entra do zero de volume.
        (_previous, _current) = (_current, _previous);
        _current.clip = clip;
        _current.time = 0f;
        _current.volume = 0f;
        _current.Play();
        _fade = 0f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Tempo sem escala: com o jogo pausado a música ainda precisa se mover.
        _fade = Mathf.MoveTowards(_fade, 1f, dt / crossfadeSeconds);

        bool paused = Time.timeScale <= 0.001f;
        _duck = Mathf.MoveTowards(_duck, paused ? pausedLevel : 1f, dt * 2f);

        var manager = RewindManager.Instance;
        bool rewinding = manager != null && manager.IsRewinding;
        // Cai rápido ao apertar e volta um pouco mais devagar ao soltar.
        _pitch = Mathf.MoveTowards(_pitch, rewinding ? rewindPitch : 1f, dt * (rewinding ? 2.5f : 1.6f));

        bool hushed = Time.unscaledTime < _hushUntil;
        _hush = Mathf.MoveTowards(_hush, hushed ? 0f : 1f, dt * (hushed ? 4f : 0.6f));

        float level = musicVolume * _duck * _hush;
        _current.volume = level * _fade;
        _current.pitch = _pitch;

        if (_previous.isPlaying)
        {
            _previous.volume = level * (1f - _fade);
            _previous.pitch = _pitch;
            if (_fade >= 1f) _previous.Stop();
        }
    }
}
