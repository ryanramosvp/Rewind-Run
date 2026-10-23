using UnityEngine;

/// <summary>
/// Mata só enquanto a prensa está descendo.
///
/// A <see cref="KillZone"/> não serve aqui: ela mata a qualquer toque, e uma
/// prensa parada no chão é um muro, não um perigo. Encostar na lateral de um
/// bloco imóvel e morrer é injusto porque contradiz o que se vê - o bloco não
/// está fazendo nada.
///
/// O que mata é o esmagamento, então a condição é a velocidade de descida. Com
/// o bloco parado em cima, parado embaixo ou subindo, ele volta a ser só
/// cenário sólido em que se esbarra.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CrusherHazard : MonoBehaviour
{
    [Tooltip("Velocidade de descida a partir da qual o bloco mata. Abaixo " +
             "disso ele é apenas um obstáculo sólido.")]
    [SerializeField, Min(0f)] float lethalSpeed = 2f;

    MovingPlatform _platform;

    void Awake()
    {
        _platform = GetComponentInParent<MovingPlatform>();

        // Precisa ser gatilho: o volume letal fica DENTRO do bloco sólido, e
        // dois colliders maciços no mesmo lugar brigariam entre si.
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
    }

    void OnTriggerEnter(Collider other) => TryCrush(other);

    // Rede de segurança: se a entrada cair justo num quadro em que a prensa
    // ainda não pegou velocidade, o quadro seguinte ainda pega.
    void OnTriggerStay(Collider other) => TryCrush(other);

    void TryCrush(Collider other)
    {
        if (_platform == null) return;

        // No rewind a prensa anda para trás, e uma subida desfeita é uma
        // descida na tela - sem esta trava ela mataria quem só está voltando
        // no tempo.
        if (_platform.IsRewinding) return;
        if (_platform.Velocity.y > -lethalSpeed) return;

        var respawn = LevelRespawn.Instance;
        if (respawn == null) return;

        if (other.GetComponentInParent<PlayerController>() != null) respawn.RestartLevel();
    }
}
