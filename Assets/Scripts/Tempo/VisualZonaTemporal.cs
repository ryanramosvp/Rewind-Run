using UnityEngine;

/// <summary>
/// O que se vê e se ouve de uma <see cref="ZonaTemporal"/>: a esfera de âmbar,
/// os anéis girando devagar e o zumbido grave.
///
/// Fica separado da zona porque é só aparência: a zona decide o tempo, este
/// componente mostra onde ela está.
///
/// Os anéis giram no tempo da própria zona - devagar, como tudo lá dentro.
/// O zumbido é 3D: cresce ao chegar perto da borda e fica cheio dentro, então
/// o ouvido avisa a entrada antes do olho.
/// </summary>
[RequireComponent(typeof(ZonaTemporal))]
public class VisualZonaTemporal : MonoBehaviour
{
    [Tooltip("Anéis que giram. Cada um gira num eixo próprio.")]
    [SerializeField] Transform[] aneis = new Transform[0];

    [Tooltip("Graus por segundo dos anéis, fora da zona (dentro, a escala dela se aplica).")]
    [SerializeField] float giro = 40f;

    [Tooltip("Esfera translúcida. Pulsa de leve.")]
    [SerializeField] Transform esfera;

    [SerializeField] AudioSource zumbido;

    ZonaTemporal _zona;
    Vector3 _escalaEsfera;

    void Awake()
    {
        _zona = GetComponent<ZonaTemporal>();
        if (esfera != null) _escalaEsfera = esfera.localScale;

        if (zumbido != null)
        {
            zumbido.clip = SomProcedural.Zumbido;
            zumbido.loop = true;
            zumbido.spatialBlend = 1f;
            zumbido.rolloffMode = AudioRolloffMode.Linear;
            zumbido.minDistance = _zona.Raio * 0.8f;
            zumbido.maxDistance = _zona.Raio * 1.8f;
            zumbido.dopplerLevel = 0f;
            zumbido.Play();
        }
    }

    void Update()
    {
        // Pausa congela junto (deltaTime zero); rewind não importa para um enfeite.
        float passo = Time.deltaTime * giro * _zona.Escala;
        for (int i = 0; i < aneis.Length; i++)
        {
            if (aneis[i] == null) continue;
            var eixo = (i % 3) switch { 0 => Vector3.up, 1 => Vector3.right, _ => Vector3.forward };
            aneis[i].Rotate(eixo, passo * (i % 2 == 0 ? 1f : -0.7f), Space.Self);
        }

        if (esfera != null)
            esfera.localScale = _escalaEsfera * (1f + 0.015f * Mathf.Sin(Time.time * 1.3f));
    }
}
