using UnityEngine;

/// <summary>
/// Gira um perigo em torno de um eixo, em velocidade constante.
///
/// A <see cref="MovingPlatform"/> só anda em linha reta, e linha reta não
/// resolve o obstáculo que esta fase precisa: uma hélice que varre o corredor
/// inteiro. Varredura exige rotação, e rotação em volta de um pivô não se
/// escreve como deslocamento.
///
/// Velocidade constante, sem suavizar as pontas, pela mesma razão da plataforma
/// móvel: para atravessar o jogador precisa prever onde a lâmina vai estar, e
/// aceleração variável transforma previsão em adivinhação.
///
/// Durante o rewind o relógio anda para trás em vez de congelar. Sai mais
/// barato que gravar amostras - o ângulo é função pura do tempo, então inverter
/// o tempo já devolve a rotação exata, sem buffer e sem erro acumulado.
/// </summary>
[DefaultExecutionOrder(-200)]
public class RotatingHazard : MonoBehaviour
{
    [Tooltip("Eixo de giro, no espaço local do objeto.")]
    [SerializeField] Vector3 axis = Vector3.up;

    [Tooltip("Graus por segundo. Negativo gira para o outro lado.")]
    [SerializeField] float degreesPerSecond = 90f;

    [Tooltip("Defasagem inicial, de 0 a 1 da volta. É o que impede várias " +
             "hélices de girarem em bloco.")]
    [SerializeField, Range(0f, 1f)] float phase;

    /// <summary>Sentido e velocidade do giro. Quem empurra precisa saber para que lado.</summary>
    public float DegreesPerSecond => degreesPerSecond;

    /// <summary>Eixo do giro já normalizado, em espaço de mundo.</summary>
    public Vector3 WorldAxis => transform.TransformDirection(_axis);

    Quaternion _rest;
    Vector3 _axis;
    float _time;

    void Awake()
    {
        _rest = transform.localRotation;
        _axis = axis.sqrMagnitude < 0.0001f ? Vector3.up : axis.normalized;
    }

    void Update()
    {
        var manager = RewindManager.Instance;

        if (manager != null && manager.IsRewinding) _time -= Time.deltaTime * manager.RewindSpeed;
        else _time += Time.deltaTime;

        float angle = phase * 360f + degreesPerSecond * _time;
        transform.localRotation = _rest * Quaternion.AngleAxis(angle, _axis);
    }
}
