using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Câmera do menu: orbita o cenário e, de tempos em tempos, rebobina.
///
/// A ideia é o menu se apresentar com a própria mecânica do jogo em vez de
/// explicá-la. A cada poucos segundos a imagem ganha o efeito de rewind e a
/// órbita volta correndo para trás, como uma fita sendo rebobinada - quem olha
/// entende o que o jogo faz antes de apertar qualquer botão.
/// </summary>
public class MenuDiorama : MonoBehaviour
{
    [SerializeField] Transform target;

    [Tooltip("Volume com o perfil do rewind. Mesmo perfil usado em jogo.")]
    [SerializeField] Volume rewindVolume;

    [Header("Órbita")]
    [SerializeField] float orbitSpeed = 7f;
    [SerializeField, Min(1f)] float radius = 34f;
    [SerializeField] float height = 18f;
    [SerializeField] Vector3 lookOffset = new Vector3(0f, 6f, 0f);

    [Header("Rebobinagem")]
    [Tooltip("Intervalo entre uma rebobinada e a seguinte.")]
    [SerializeField, Min(2f)] float every = 9f;

    [SerializeField, Min(0.2f)] float duration = 1.5f;

    [Tooltip("Quantas vezes mais rápido a órbita corre para trás.")]
    [SerializeField, Min(1f)] float rewindSpeedScale = 6f;

    float _angle;
    float _timer;
    float _rewindLeft;
    float _weight;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (_rewindLeft > 0f)
        {
            _rewindLeft -= dt;
            _angle -= orbitSpeed * rewindSpeedScale * dt;
        }
        else
        {
            _timer += dt;
            if (_timer >= every)
            {
                _timer = 0f;
                _rewindLeft = duration;
            }

            _angle += orbitSpeed * dt;
        }

        // Entra rápido e sai devagar, como em jogo: o começo precisa ser
        // notado, o fim não deve ser um corte.
        float target01 = _rewindLeft > 0f ? 1f : 0f;
        float speed = target01 > _weight ? 1f / 0.12f : 1f / 0.5f;
        _weight = Mathf.MoveTowards(_weight, target01, speed * dt);

        if (rewindVolume != null) rewindVolume.weight = _weight;

        Vector3 center = target != null ? target.position : Vector3.zero;
        var rotation = Quaternion.Euler(0f, _angle, 0f);

        transform.position = center + rotation * new Vector3(0f, height, -radius);
        transform.LookAt(center + lookOffset);
    }
}
