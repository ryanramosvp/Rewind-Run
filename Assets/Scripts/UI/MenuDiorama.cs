using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Câmera do menu: orbita o cenário enquanto ele se desfaz, e rebobina.
///
/// A ideia é o menu se apresentar com a própria mecânica do jogo em vez de
/// explicá-la. O cenário se desmancha devagar; quando já está todo solto, a
/// imagem ganha o efeito de rewind e tudo volta - a órbita e cada peça - para o
/// instante em que ele ainda estava inteiro. Quem olha entende o que o jogo faz
/// antes de apertar qualquer botão.
///
/// Câmera e estrutura voltam para o MESMO instante, e é isso que vende o efeito:
/// se só as peças voltassem, pareceria uma animação de montagem; com a órbita
/// recuando junto, parece o tempo andando para trás.
/// </summary>
public class MenuDiorama : MonoBehaviour
{
    [SerializeField] Transform target;

    [Tooltip("Volume com o perfil do rewind. Mesmo perfil usado em jogo.")]
    [SerializeField] Volume rewindVolume;

    [Tooltip("Cenário que se desfaz e se remonta.")]
    [SerializeField] DioramaDissolve dissolve;

    [Header("Órbita")]
    [SerializeField] float orbitSpeed = 7f;
    [SerializeField, Min(1f)] float radius = 34f;
    [SerializeField] float height = 18f;
    [SerializeField] Vector3 lookOffset = new Vector3(0f, 6f, 0f);

    [Header("Ciclo")]
    [Tooltip("Tempo com o cenário inteiro antes de começar a se soltar.")]
    [SerializeField, Min(0f)] float holdIntact = 1.6f;

    [Tooltip("Duração do desmanche. Longo de propósito: é para ser notado aos poucos.")]
    [SerializeField, Min(0.5f)] float dissolveSeconds = 9f;

    [Tooltip("Pausa com tudo solto, para o desmanche completo ser visto.")]
    [SerializeField, Min(0f)] float holdApart = 0.9f;

    [Tooltip("Duração da rebobinada. Curta: o rewind tem de parecer um tranco.")]
    [SerializeField, Min(0.2f)] float rewindSeconds = 1.6f;

    enum Phase { Intact, Dissolving, Apart, Rewinding }

    Phase _phase = Phase.Intact;
    float _t;
    float _angle;
    float _angleWhenIntact;
    float _angleAtRewind;
    float _weight;

    void Start()
    {
        if (dissolve != null) dissolve.SetProgress(0f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _t += dt;

        float u = 0f;

        switch (_phase)
        {
            case Phase.Intact:
                _angle += orbitSpeed * dt;
                if (_t >= holdIntact) Enter(Phase.Dissolving);
                break;

            case Phase.Dissolving:
                _angle += orbitSpeed * dt;
                u = Mathf.Clamp01(_t / dissolveSeconds);
                if (_t >= dissolveSeconds) Enter(Phase.Apart);
                break;

            case Phase.Apart:
                _angle += orbitSpeed * dt;
                u = 1f;
                if (_t >= holdApart) Enter(Phase.Rewinding);
                break;

            case Phase.Rewinding:
                float k = Mathf.Clamp01(_t / rewindSeconds);
                k = k * k * (3f - 2f * k);
                u = 1f - k;
                _angle = Mathf.Lerp(_angleAtRewind, _angleWhenIntact, k);
                if (_t >= rewindSeconds) Enter(Phase.Intact);
                break;
        }

        if (dissolve != null) dissolve.SetProgress(u);

        // Entra rápido e sai devagar, como em jogo: o começo precisa ser
        // notado, o fim não deve ser um corte.
        float target01 = _phase == Phase.Rewinding ? 1f : 0f;
        float speed = target01 > _weight ? 1f / 0.12f : 1f / 0.5f;
        _weight = Mathf.MoveTowards(_weight, target01, speed * dt);
        if (rewindVolume != null) rewindVolume.weight = _weight;

        Vector3 center = target != null ? target.position : Vector3.zero;
        var rotation = Quaternion.Euler(0f, _angle, 0f);

        transform.position = center + rotation * new Vector3(0f, height, -radius);
        transform.LookAt(center + lookOffset);
    }

    void Enter(Phase next)
    {
        // O último instante inteiro é o começo do desmanche: é para lá que o
        // rewind leva. Fora dele a órbita segue em frente, então a cada volta
        // do ciclo a câmera avança um pouco ao redor do cenário.
        if (next == Phase.Dissolving) _angleWhenIntact = _angle;
        if (next == Phase.Rewinding) _angleAtRewind = _angle;

        if (next == Phase.Intact) _angle = _angleWhenIntact;

        _phase = next;
        _t = 0f;
    }
}
