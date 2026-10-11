using UnityEngine;

/// <summary>
/// Barra giratória que arremessa quem encosta, em vez de matar.
///
/// A morte continua sendo a lava: a barra só decide para onde a bruxa vai, e o
/// mapa decide se aquilo foi fatal. Quem é pego perto do meio do corredor cai
/// de pé e perde um segundo; quem é pego na beirada vai para fora. A punição
/// passa a ser proporcional ao erro, o que uma morte por toque nunca consegue.
///
/// A força saiu de 14 para 12 m/s depois de jogar: na mão o arremesso jogava
/// longe demais e parecia castigo, não contratempo.
///
/// Vale saber que a recuperação tem um degrau, não uma rampa. Em 14 nem
/// reagindo se escapa da beirada; em 13 já se escapa. Qualquer alívio, por
/// menor que fosse, ia cair do outro lado desse degrau - então 12 foi escolhido
/// pela sensação do arremesso, não por preservar a letalidade na borda, que
/// já estava perdida de qualquer jeito.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BladePush : MonoBehaviour
{
    [Tooltip("Velocidade do arremesso. É número de sensação, não de física: a " +
             "ponta da barra anda a uns 4 m/s, o que mal moveria alguém.")]
    [SerializeField, Min(0f)] float pushSpeed = 12f;

    [Tooltip("Altura do tranco para cima. Serve para tirar os pés do chão - " +
             "de pé, a bruxa freia o empurrão quase na hora.")]
    [SerializeField, Min(0f)] float riseHeight = 1.3f;

    [Tooltip("Tempo sem controle depois da pancada.")]
    [SerializeField, Min(0f)] float controlLock = 0.30f;

    [Tooltip("Intervalo mínimo entre duas pancadas, para a barra não bater " +
             "duas vezes na mesma passagem.")]
    [SerializeField, Min(0f)] float cooldown = 0.4f;

    RotatingHazard _hazard;
    float _nextPush;

    void Awake()
    {
        _hazard = GetComponent<RotatingHazard>();
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time < _nextPush) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        // Durante o rewind a barra está sendo desenhada para trás; empurrar aí
        // brigaria com quem está restaurando a posição da bruxa.
        var manager = RewindManager.Instance;
        if (manager != null && manager.IsRewinding) return;

        _nextPush = Time.time + cooldown;
        player.Knockback(Direction(player.transform.position) * pushSpeed, riseHeight, controlLock);
    }

    /// <summary>
    /// Para onde a pancada joga: mistura de tangente com radial.
    ///
    /// Tangente sozinha é o que a física diria, mas falha no caso que importa -
    /// pego de lado, a tangente aponta ao longo do corredor e a bruxa escapa
    /// ilesa. Radial sozinha falha no caso oposto. Somadas, sobra sempre uma
    /// componente apontando para fora, que é o que a barra deve fazer.
    /// </summary>
    Vector3 Direction(Vector3 target)
    {
        Vector3 axis = _hazard != null ? _hazard.WorldAxis : Vector3.up;

        Vector3 radial = Vector3.ProjectOnPlane(target - transform.position, axis);
        if (radial.sqrMagnitude < 0.0001f) radial = transform.right;
        radial.Normalize();

        float spin = _hazard != null ? Mathf.Sign(_hazard.DegreesPerSecond) : 1f;
        Vector3 tangent = Vector3.Cross(axis, radial).normalized * spin;

        Vector3 push = Vector3.ProjectOnPlane(tangent * 0.8f + radial * 0.6f, Vector3.up);
        return push.sqrMagnitude < 0.0001f ? radial : push.normalized;
    }
}
