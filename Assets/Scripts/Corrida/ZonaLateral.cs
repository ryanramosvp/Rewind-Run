using UnityEngine;

/// <summary>
/// Trecho em 2D: dentro da área, a câmera gira para uma vista de lado e a
/// bruxa fica presa num plano vertical; ao sair, tudo volta ao 3D.
///
/// Não é um segundo sistema de movimento: é o mesmo controle, com o input
/// de profundidade descartado e a posição corrigida para o plano (ver
/// <see cref="PlayerController.PrenderNoPlano"/>). Correr, pular, pulo
/// duplo, impulso e gravidade invertida funcionam igual.
///
/// A área é conferida pela posição a cada quadro, e não por gatilho: o
/// rewind e o renascimento movem a bruxa sem física, e um OnTriggerExit
/// perdido deixaria a câmera presa de lado no resto da fase.
/// </summary>
public class ZonaLateral : MonoBehaviour
{
    [Tooltip("Área do trecho, em coordenadas do mundo.")]
    [SerializeField] Bounds area;

    [Tooltip("Normal do plano, horizontal, apontando para onde a câmera fica.")]
    [SerializeField] Vector3 normalDoPlano = Vector3.right;

    [Tooltip("Inclinação da câmera lateral, em graus. Baixa: a vista é de lado.")]
    [SerializeField, Range(0f, 40f)] float inclinacao = 6f;

    [SerializeField, Min(5f)] float distancia = 22f;

    [Tooltip("Segundos da virada de câmera, na entrada e na saída.")]
    [SerializeField, Min(0.1f)] float transicao = 0.8f;

    PlayerController _bruxa;
    OrbitCamera _camera;
    bool _dentro;

    void Start()
    {
        _bruxa = FindAnyObjectByType<PlayerController>();
        _camera = FindAnyObjectByType<OrbitCamera>();
    }

    void LateUpdate()
    {
        if (_bruxa == null) return;

        bool dentro = area.Contains(_bruxa.transform.position);
        if (dentro == _dentro) return;
        _dentro = dentro;

        if (dentro)
        {
            _bruxa.PrenderNoPlano(normalDoPlano, area.center);
            // A câmera fica do lado da normal, olhando para o plano.
            var olhar = -normalDoPlano;
            float yaw = Mathf.Atan2(olhar.x, olhar.z) * Mathf.Rad2Deg;
            if (_camera != null) _camera.ForcarEnquadramento(yaw, inclinacao, distancia, transicao);
        }
        else
        {
            _bruxa.SoltarDoPlano();
            if (_camera != null) _camera.LiberarEnquadramento();
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.35f);
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
