using System.Collections;
using UnityEngine;

/// <summary>
/// A linha de chegada da corrida. Tocada antes do fantasma, encerra a
/// disputa: o tempo da fase é registrado, o fantasma para, olha para a bruxa
/// e se desfaz, a música muda e um portal se abre.
///
/// O tempo da fase termina aqui, e não no epílogo: é o tempo da corrida, e o
/// passeio depois dela não pode contar.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ChegadaDaCorrida : MonoBehaviour
{
    [Tooltip("Portal que se abre depois que o fantasma se desfaz. Começa desligado.")]
    [SerializeField] GameObject portal;

    [Tooltip("Música da vitória. Vazio mantém a atual, mais baixa.")]
    [SerializeField] AudioClip musicaDaVitoria;

    [SerializeField] AudioSource som;

    bool _venceu;

    void Reset()
    {
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
    }

    void Awake()
    {
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
        if (portal != null) portal.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_venceu) return;
        var bruxa = other.GetComponentInParent<PlayerController>();
        if (bruxa == null) return;

        var fantasma = FantasmaDaCorrida.Instancia;
        if (fantasma != null && fantasma.TempoDaCorrida >= fantasma.Duracao) return;

        _venceu = true;
        StartCoroutine(Vencer(bruxa, fantasma));
    }

    IEnumerator Vencer(PlayerController bruxa, FantasmaDaCorrida fantasma)
    {
        var sessao = GameSession.Instance;
        if (sessao != null) sessao.Finish();

        if (fantasma != null) fantasma.Parar(bruxa.transform.position);
        if (som != null) som.Play();
        MusicPlayer.Hush(3f);
        if (musicaDaVitoria != null) MusicPlayer.Tocar(musicaDaVitoria);

        // Espera o fantasma olhar e se desfazer antes de abrir a passagem.
        yield return new WaitForSeconds(3.2f);
        if (portal != null)
        {
            portal.SetActive(true);
            portal.transform.localScale = Vector3.zero;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.8f;
                float s = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                portal.transform.localScale = Vector3.one * s;
                yield return null;
            }
        }
    }
}
