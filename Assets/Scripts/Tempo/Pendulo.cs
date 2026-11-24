using UnityEngine;

/// <summary>
/// Pêndulo de relógio que corta o caminho: balança de um lado a outro e
/// arremessa para fora quem estiver na frente do peso.
///
/// Anda no <see cref="RelogioLocal"/>, então uma zona de âmbar o deixa lento a
/// ponto de se passar por baixo com calma, e o rewind o devolve ao ângulo
/// exato. O balanço é um seno: mais rápido no meio, quase parado nas pontas -
/// as pontas são a janela, e o olho a lê sem precisar decorar.
///
/// Não mata no toque: como as barras do vulcão, só decide para onde a bruxa
/// vai, e o mapa decide se foi fatal. O peso é grande e pesado, então o
/// arremesso é mais forte que o das barras e sai na direção do balanço - quem é
/// pego no meio da passagem costuma ir para fora da plataforma.
/// </summary>
[RequireComponent(typeof(RelogioLocal))]
public class Pendulo : MonoBehaviour
{
    [Tooltip("Abertura do balanço para cada lado, em graus.")]
    [SerializeField, Range(5f, 85f)] float amplitude = 60f;

    [Tooltip("Segundos de uma ida e volta completa, fora de zona.")]
    [SerializeField, Min(0.2f)] float periodo = 2.4f;

    [Tooltip("Defasagem, de 0 a 1 do período.")]
    [SerializeField, Range(0f, 1f)] float fase;

    [Tooltip("Eixo do balanço, local. Z balança de um lado para o outro do caminho.")]
    [SerializeField] Vector3 eixo = Vector3.forward;

    [Tooltip("Gatilho no peso.")]
    [SerializeField] Collider peso;

    [Tooltip("Velocidade do arremesso, em m/s.")]
    [SerializeField, Min(0f)] float forca = 15f;

    [Tooltip("Altura do tranco para cima: tira os pés do chão para o empurrão não ser freado.")]
    [SerializeField, Min(0f)] float alturaDoTranco = 1.6f;

    [Tooltip("Tempo sem controle depois da pancada.")]
    [SerializeField, Min(0f)] float semControle = 0.4f;

    [Tooltip("Intervalo mínimo entre duas pancadas.")]
    [SerializeField, Min(0f)] float intervalo = 0.5f;

    RelogioLocal _relogio;
    Quaternion _repouso;
    float _proximaPancada;

    void Awake()
    {
        _relogio = GetComponent<RelogioLocal>();
        _repouso = transform.localRotation;
        if (peso != null) peso.isTrigger = true;
    }

    float Fase => 2f * Mathf.PI * (_relogio.Tempo / periodo + fase);

    void Update()
    {
        float angulo = amplitude * Mathf.Sin(Fase);
        transform.localRotation = _repouso * Quaternion.AngleAxis(angulo, eixo.normalized);
    }

    /// <summary>Chamado pelo gatilho do peso (ver <see cref="GatilhoDoPendulo"/>).</summary>
    public void Atingiu(Collider other)
    {
        if (Time.time < _proximaPancada) return;

        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        _proximaPancada = Time.time + intervalo;
        player.Knockback(Direcao(player.transform.position) * forca, alturaDoTranco, semControle);
    }

    /// <summary>
    /// Para onde o peso está indo, no plano do chão. Girar +θ em torno de Z
    /// leva o peso (pendurado em -Y) para +X local; o sinal da velocidade é o
    /// do cosseno da fase. Perto das pontas o peso quase para, e aí vale o lado
    /// em que a bruxa está em relação ao eixo.
    /// </summary>
    Vector3 Direcao(Vector3 jogador)
    {
        var referencia = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * _repouso;
        var lado = referencia * Vector3.Cross(eixo.normalized, Vector3.down);
        lado.y = 0f;
        if (lado.sqrMagnitude < 0.0001f) lado = referencia * Vector3.right;
        lado.Normalize();

        float velocidade = Mathf.Cos(Fase);
        if (Mathf.Abs(velocidade) > 0.25f) return lado * Mathf.Sign(velocidade);

        float qualLado = Vector3.Dot(jogador - transform.position, lado);
        return lado * (qualLado >= 0f ? 1f : -1f);
    }
}
