using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A derrota da corrida: uma estrela colapsa e explode, e a onda de choque
/// atravessa o espaço até engolir a bruxa - tudo fica branco, e o tempo
/// recomeça do zero.
///
/// O jogo continua andando (é para ser visto), mas sem controle, sem pausa
/// e sem rewind: não há como fugir dela. A onda é calculada para alcançar a
/// câmera num tempo fixo, seja qual for a distância, então a sequência tem
/// sempre o mesmo ritmo: colapso, explosão, a onda chegando, branco.
///
/// Os objetos vêm prontos da cena, desligados; <see cref="Explodir"/> os
/// liga e posiciona.
/// </summary>
public class Supernova : MonoBehaviour
{
    [SerializeField] Transform nucleo;
    [Tooltip("Esfera de raio 0,5: a onda de choque. Escalada pelo raio do momento.")]
    [SerializeField] Transform onda;
    [SerializeField] Renderer ondaRenderer;
    [SerializeField] ParticleSystem faiscas;
    [SerializeField] Light luz;
    [Tooltip("Imagem branca de tela inteira, por cima de tudo.")]
    [SerializeField] Image clarao;
    [SerializeField] AudioSource som;

    [Header("Ritmo")]
    [SerializeField, Min(0.1f)] float colapso = 0.9f;
    [Tooltip("Segundos entre a explosão e a onda alcançar a câmera.")]
    [SerializeField, Min(0.5f)] float ateAtingir = 2.4f;
    [Tooltip("Segundos no branco, com o zumbido, antes de recomeçar.")]
    [SerializeField, Min(0f)] float noBranco = 1.6f;

    [Tooltip("Distância máxima da estrela até a bruxa: mais longe, a névoa a esconderia.")]
    [SerializeField, Min(20f)] float distanciaMaxima = 140f;

    static readonly int Base = Shader.PropertyToID("_BaseColor");

    /// <summary>
    /// Acende a estrela perto de <paramref name="origem"/> e solta a onda.
    /// <paramref name="aoTerminar"/> é chamado com a tela branca.
    /// </summary>
    public void Explodir(Vector3 origem, Transform bruxa, System.Action aoTerminar)
    {
        var camera = Camera.main;
        var referencia = bruxa != null ? bruxa.position : (camera != null ? camera.transform.position : origem);

        // Longe demais, a névoa a esconderia: ela nasce adiante, na mesma direção.
        var paraEla = origem - referencia;
        if (paraEla.magnitude > distanciaMaxima)
            origem = referencia + paraEla.normalized * distanciaMaxima;
        origem += Vector3.up * 12f;

        transform.position = origem;
        gameObject.SetActive(true);
        StartCoroutine(Sequencia(camera, aoTerminar));
    }

    IEnumerator Sequencia(Camera camera, System.Action aoTerminar)
    {
        if (onda != null) onda.gameObject.SetActive(false);
        if (clarao != null) clarao.color = new Color(1f, 1f, 1f, 0f);
        if (som != null)
        {
            som.clip = SomProcedural.Supernova;
            som.Play();
        }

        var orbita = camera != null ? camera.GetComponent<OrbitCamera>() : null;
        if (orbita != null) orbita.enabled = false;
        var olharDe = camera != null ? camera.transform.rotation : Quaternion.identity;

        // Colapso: a estrela encolhe e a luz cresce; a câmera vira para ela.
        float t = 0f;
        while (t < colapso)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / colapso);
            if (nucleo != null) nucleo.localScale = Vector3.one * Mathf.Lerp(14f, 1.5f, u * u);
            if (luz != null) luz.intensity = Mathf.Lerp(2f, 14f, u);
            Olhar(camera, olharDe, u, 0f);
            yield return null;
        }

        // Explosão: a onda sai do centro e cresce até a câmera.
        if (nucleo != null) nucleo.gameObject.SetActive(false);
        if (onda != null) onda.gameObject.SetActive(true);
        float distancia = camera != null ? Vector3.Distance(camera.transform.position, transform.position) : 50f;
        float velocidade = distancia / ateAtingir;
        if (faiscas != null)
        {
            var main = faiscas.main;
            main.startSpeed = velocidade * 1.05f;
            main.startLifetime = ateAtingir;
            faiscas.Play();
        }

        var bloco = new MaterialPropertyBlock();
        float r = 0f;
        while (r < distancia)
        {
            float dt = Time.unscaledDeltaTime;
            r += velocidade * dt;
            float perto = Mathf.Clamp01(r / distancia);
            if (onda != null) onda.localScale = Vector3.one * r * 2f;
            if (ondaRenderer != null)
            {
                // Azul-branca na saída, esquentando para laranja ao chegar.
                var cor = Color.Lerp(new Color(0.7f, 0.9f, 1f, 0.45f), new Color(1f, 0.75f, 0.45f, 0.75f), perto);
                bloco.SetColor(Base, cor);
                ondaRenderer.SetPropertyBlock(bloco);
            }

            if (luz != null)
            {
                luz.range = r * 1.5f + 20f;
                luz.intensity = Mathf.Lerp(14f, 30f, perto);
            }

            Olhar(camera, olharDe, 1f, perto * perto * 0.6f);
            if (clarao != null) clarao.color = new Color(1f, 1f, 1f, Mathf.Pow(perto, 6f) * 0.6f);
            yield return null;
        }

        // Atingiu: branco de uma vez, o estrondo corta e só sobra o zumbido.
        if (clarao != null) clarao.color = Color.white;
        if (som != null)
        {
            som.Stop();
            som.PlayOneShot(SomProcedural.Zumbido_Agudo, 0.5f);
        }

        yield return new WaitForSecondsRealtime(noBranco);
        aoTerminar?.Invoke();
    }

    void Olhar(Camera camera, Quaternion de, float u, float tremor)
    {
        if (camera == null) return;
        var alvo = Quaternion.LookRotation(transform.position - camera.transform.position);
        var rotacao = Quaternion.Slerp(de, alvo, u * u * (3f - 2f * u));
        if (tremor > 0f)
            rotacao *= Quaternion.Euler(Random.Range(-1f, 1f) * tremor * 2f, Random.Range(-1f, 1f) * tremor * 2f, 0f);
        camera.transform.rotation = rotacao;
    }
}
