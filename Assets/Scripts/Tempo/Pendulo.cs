using UnityEngine;

/// <summary>
/// Pêndulo de relógio que corta o caminho: balança de um lado a outro e
/// derruba a bruxa que estiver na frente do peso.
///
/// Anda no <see cref="RelogioLocal"/>, então uma zona de âmbar o deixa lento a
/// ponto de se passar por baixo com calma, e o rewind o devolve ao ângulo
/// exato. O balanço é um seno: mais rápido no meio, quase parado nas pontas -
/// as pontas são a janela, e o olho a lê sem precisar decorar.
///
/// Mata como as bolas gigantes, e não empurra como as hélices: o peso é
/// grande e lento, e a leitura "não fique embaixo dele" é a mais clara.
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

    RelogioLocal _relogio;
    Quaternion _repouso;
    int _ultimoQuadroDeMorte = -1;

    void Awake()
    {
        _relogio = GetComponent<RelogioLocal>();
        _repouso = transform.localRotation;
        if (peso != null) peso.isTrigger = true;
    }

    void Update()
    {
        float angulo = amplitude * Mathf.Sin(2f * Mathf.PI * (_relogio.Tempo / periodo + fase));
        transform.localRotation = _repouso * Quaternion.AngleAxis(angulo, eixo.normalized);
    }

    /// <summary>Chamado pelo gatilho do peso (ver <see cref="GatilhoDoPendulo"/>).</summary>
    public void Atingiu(Collider other)
    {
        if (_ultimoQuadroDeMorte == Time.frameCount) return;

        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        var respawn = LevelRespawn.Instance;
        if (respawn == null) return;

        _ultimoQuadroDeMorte = Time.frameCount;
        respawn.RestartLevel();
    }
}
