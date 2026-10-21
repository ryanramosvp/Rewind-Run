using UnityEngine;

/// <summary>
/// Câmera fixa que olha sempre de fora para dentro de um ponto central.
///
/// Existe para o tutorial, que acontece em volta do vulcão do menu. Com um
/// ângulo só, metade da escada em espiral ficaria escondida atrás da torre -
/// e numa subida o pedaço escondido é sempre o próximo degrau. Olhando de
/// fora, a bruxa fica sempre entre a câmera e a torre, e a torre vira fundo
/// em vez de obstáculo.
///
/// O ângulo anda em fatias, não acompanha cada passo. A câmera contínua
/// deixaria a base do movimento travada o tempo todo (ver
/// PlayerController.UpdateMovementBasis), e "para frente" mudaria a cada
/// metro. Em fatias ela se comporta como uma sequência de zonas de câmera,
/// que é o que o resto do jogo já faz.
/// </summary>
public class RadialCameraFraming : MonoBehaviour
{
    [SerializeField] OrbitCamera cameraRig;
    [SerializeField] Transform player;
    [SerializeField] Vector3 center;

    [Tooltip("Em quantas fatias a volta se divide. Seis casa com os degraus da espiral.")]
    [SerializeField, Range(3, 12)] int sectors = 6;

    [Tooltip("Folga, em graus, antes de trocar de fatia. Sem ela, parar em cima da " +
             "divisa faria a câmera ir e voltar sem parar.")]
    [SerializeField, Range(0f, 20f)] float hysteresis = 8f;

    [SerializeField, Range(5f, 70f)] float pitch = 30f;
    [SerializeField, Min(3f)] float distance = 19f;
    [SerializeField, Min(0.1f)] float blendSeconds = 0.9f;

    int _sector = -1;

    void Start()
    {
        if (cameraRig == null) cameraRig = FindAnyObjectByType<OrbitCamera>();
        if (player == null)
        {
            var controller = FindAnyObjectByType<PlayerController>();
            if (controller != null) player = controller.transform;
        }
    }

    void LateUpdate()
    {
        if (cameraRig == null || player == null) return;

        Vector3 outward = player.position - center;
        outward.y = 0f;

        // Em cima do centro "para fora" não tem direção; fica a fatia atual.
        if (outward.sqrMagnitude < 1f) return;

        float angle = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
        float width = 360f / sectors;

        if (_sector >= 0 && Mathf.Abs(Mathf.DeltaAngle(_sector * width, angle)) <= width * 0.5f + hysteresis)
            return;

        bool first = _sector < 0;
        _sector = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / width) % sectors;

        // +180: a câmera fica do lado de fora da bruxa, olhando para o centro.
        cameraRig.SetFixedFraming(_sector * width + 180f, pitch, distance,
                                  first ? 0.05f : blendSeconds);
    }
}
