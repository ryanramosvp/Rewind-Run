using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// O fim do jogo: tocar a ampulheta no topo da última fase.
///
/// Não é uma tela de vitória. A corrida é registrada como em qualquer fase,
/// mas no lugar do placar vem uma pequena cena sem controle: a câmera se
/// afasta e sobe, a ampulheta gigante vira devagar, a imagem pulsa com o
/// efeito do rewind e um coro entra. Depois tudo clareia até o branco e
/// aparecem os resultados da campanha.
///
/// Não há história para contar, e a cena não inventa uma. Ela fecha com a
/// linguagem que o jogo inteiro usou: a ampulheta (o ponto de controle, em
/// grande), o efeito de voltar no tempo e a música. Quem jogou entende que
/// virar a ampulheta é recomeçar o tempo - e o botão "Jogar de novo" nos
/// resultados diz o mesmo.
///
/// Usa tempo escalado: a cena acontece com o jogo rodando, e a pausa fica
/// desligada para ninguém abrir um menu no meio dela.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FimDaCampanha : MonoBehaviour
{
    [Tooltip("A ampulheta gigante, que vira durante a cena.")]
    [SerializeField] Transform ampulheta;

    [Tooltip("Ponto para onde a câmera olha enquanto se afasta.")]
    [SerializeField] Transform foco;

    [Tooltip("Quanto a câmera sobe e se afasta, em metros.")]
    [SerializeField] float subida = 26f;
    [SerializeField] float afastamento = 48f;

    [Tooltip("Coro do fim. Entra por cima da música da fase, numa troca suave.")]
    [SerializeField] AudioClip musicaFinal;

    [Tooltip("Imagem branca de tela inteira, para o clarão final.")]
    [SerializeField] Image clarao;

    [Tooltip("Volume com o perfil do rewind. Pulsa enquanto a ampulheta vira.")]
    [SerializeField] Volume volumeRewind;

    [Tooltip("Créditos, que rolam logo depois do clarão.")]
    [SerializeField] string cenaDosCreditos = "Creditos";

    [Tooltip("Para onde os créditos levam ao terminar: os resultados da campanha.")]
    [SerializeField] string cenaDeResultados = "Resultados";

    [SerializeField, Min(1f)] float duracao = 11f;

    bool _comecou;

    void Reset()
    {
        if (TryGetComponent<Collider>(out var area)) area.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_comecou) return;

        var bruxa = other.GetComponentInParent<PlayerController>();
        if (bruxa == null) return;

        _comecou = true;
        StartCoroutine(Encerrar(bruxa));
    }

    IEnumerator Encerrar(PlayerController bruxa)
    {
        // O tempo conta até aqui, como em qualquer fase: o recorde e a nota da
        // última fase entram nos resultados como os das outras.
        var sessao = GameSession.Instance;
        if (sessao != null) sessao.Finish();
        SaveSystem.ConcluirCampanha();

        // Sem controle, sem pausa, sem rewind, sem HUD: daqui em diante é só olhar.
        bruxa.enabled = false;
        var rewind = RewindManager.Instance;
        if (rewind != null) rewind.enabled = false;
        foreach (var pausa in FindObjectsByType<PauseMenu>(FindObjectsInactive.Exclude)) pausa.enabled = false;
        var hud = FindAnyObjectByType<SpeedrunHud>();
        if (hud != null) hud.gameObject.SetActive(false);

        // O efeito de tela do rewind zera o próprio peso a cada quadro quando
        // não há rewind; desligado, quem pulsa o volume é esta cena.
        if (volumeRewind != null && volumeRewind.TryGetComponent<RewindScreenEffect>(out var efeito))
            efeito.enabled = false;

        if (musicaFinal != null) MusicPlayer.Tocar(musicaFinal);

        var camera = Camera.main;
        var orbita = camera != null ? camera.GetComponent<OrbitCamera>() : null;
        if (orbita != null) orbita.enabled = false;

        Vector3 inicioCamera = camera != null ? camera.transform.position : Vector3.zero;
        Vector3 centro = foco != null ? foco.position : transform.position;
        Vector3 daBruxa = Vector3.ProjectOnPlane(inicioCamera - centro, Vector3.up).normalized;
        if (daBruxa.sqrMagnitude < 0.01f) daBruxa = Vector3.back;
        Quaternion inicioAmpulheta = ampulheta != null ? ampulheta.rotation : Quaternion.identity;

        float t = 0f;
        while (t < duracao)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duracao);

            if (camera != null)
            {
                // Afasta e sobe girando um quarto de volta em torno do centro:
                // a fase inteira aparece embaixo, a ampulheta no meio.
                float suave = u * u * (3f - 2f * u);
                Vector3 direcao = Quaternion.AngleAxis(90f * suave, Vector3.up) * daBruxa;
                Vector3 alvo = centro + direcao * Mathf.Lerp(12f, afastamento, suave) + Vector3.up * Mathf.Lerp(4f, subida, suave);
                camera.transform.position = Vector3.Lerp(inicioCamera, alvo, Mathf.Clamp01(u * 3f));
                camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation,
                    Quaternion.LookRotation(centro - camera.transform.position), Time.deltaTime * 3f);
            }

            // A ampulheta vira no meio da cena, devagar, assentando no fim.
            float giro = Mathf.Clamp01((u - 0.2f) / 0.45f);
            giro = 1f - Mathf.Pow(1f - giro, 3f);
            if (ampulheta != null) ampulheta.rotation = Quaternion.AngleAxis(180f * giro, Vector3.forward) * inicioAmpulheta;

            // O efeito do rewind pulsa enquanto ela vira: o tempo recomeçando.
            if (volumeRewind != null)
                volumeRewind.weight = Mathf.Sin(Mathf.Clamp01((u - 0.2f) / 0.5f) * Mathf.PI) * 0.7f;

            // Clarão no último quinto.
            if (clarao != null)
            {
                var cor = clarao.color;
                cor.a = Mathf.Clamp01((u - 0.8f) / 0.2f);
                clarao.color = cor;
            }

            yield return null;
        }

        // Os créditos vêm antes dos resultados: é o fim do jogo, e quem fez
        // aparece enquanto o coro ainda soa. Eles mesmos levam aos resultados.
        TelaDeCreditos.AbrirNoFimDaCampanha(cenaDeResultados);
        SceneManager.LoadScene(cenaDosCreditos);
    }
}
