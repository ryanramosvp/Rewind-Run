using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Efeito de tela enquanto o rewind está ativo: aberração cromática, vinheta
/// azulada, granulação e perda de cor, como uma fita sendo rebobinada.
///
/// Entra e sai com tempos diferentes de propósito. A entrada é quase imediata,
/// para o jogador ver na hora que o botão respondeu; a saída é lenta, para a
/// volta ao normal não parecer um corte seco.
/// </summary>
[RequireComponent(typeof(Volume))]
public class RewindScreenEffect : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float fadeInSeconds = 0.1f;
    [SerializeField, Min(0.01f)] float fadeOutSeconds = 0.4f;

    [Header("Pulso")]
    [Tooltip("Velocidade da oscilação da aberração cromática.")]
    [SerializeField, Min(0f)] float pulseSpeed = 9f;

    [Tooltip("Quanto a aberração oscila em volta do valor do perfil.")]
    [SerializeField, Range(0f, 1f)] float pulseAmount = 0.3f;

    Volume _volume;
    ChromaticAberration _chromatic;
    float _profileChromatic;
    float _weight;

    void Awake()
    {
        _volume = GetComponent<Volume>();
        _volume.weight = 0f;

        // .profile (e não .sharedProfile) devolve uma cópia de runtime, então
        // mexer nos valores aqui não suja o asset do perfil no disco.
        var profile = _volume.profile;
        if (profile != null && profile.TryGet(out _chromatic))
            _profileChromatic = _chromatic.intensity.value;
    }

    void Update()
    {
        var manager = RewindManager.Instance;
        bool active = manager != null && manager.IsRewinding;

        float seconds = active ? fadeInSeconds : fadeOutSeconds;
        _weight = Mathf.MoveTowards(_weight, active ? 1f : 0f, Time.deltaTime / seconds);
        _volume.weight = _weight;

        if (_chromatic == null || _weight <= 0f) return;

        // A oscilação impede que o efeito vire um filtro parado na tela: é o
        // movimento que faz parecer uma fita correndo para trás.
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
        _chromatic.intensity.value = _profileChromatic * pulse;
    }
}
