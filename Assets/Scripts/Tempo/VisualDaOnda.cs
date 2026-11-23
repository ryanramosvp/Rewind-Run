using UnityEngine;

/// <summary>
/// A imagem da onda do <see cref="CicloDoColapso"/>.
///
/// A onda é uma esfera, não um círculo no chão: uma casca translúcida
/// azul-clara do tamanho do raio de agora, e anéis luminosos onde ela corta
/// algumas alturas da arena. Assim a frente aparece igual numa ilha baixa e
/// numa torre alta - e é a mesma esfera que decide quem se solta.
///
/// Partículas nascem na superfície da esfera: na ida saem para fora, na
/// volta são puxadas para o centro, para a volta ler como atração.
///
/// O núcleo da ampulheta acende com a carga: quase apagado em repouso,
/// crescendo na preparação, cheio enquanto a onda existe. Quem olha para a
/// ampulheta sabe em que etapa o ciclo está.
///
/// Só lê o ciclo; não guarda estado. No rewind a imagem volta junto.
/// </summary>
[RequireComponent(typeof(CicloDoColapso))]
public class VisualDaOnda : MonoBehaviour
{
    [Tooltip("Esfera de raio 0,5 (a primitiva do Unity). Escala pelo raio da onda.")]
    [SerializeField] Transform casca;

    [Tooltip("Anéis de raio 1 deitados em XZ, um para cada altura em alturasDosAneis.")]
    [SerializeField] Transform[] aneis = new Transform[0];

    [Tooltip("Altura de cada anel em relação ao centro da onda, em metros.")]
    [SerializeField] float[] alturasDosAneis = new float[0];

    [SerializeField] ParticleSystem particulas;

    [Tooltip("Brilho do núcleo da ampulheta.")]
    [SerializeField] Renderer nucleo;

    [SerializeField] Light luz;

    [Tooltip("Intensidade da luz com a carga cheia.")]
    [SerializeField, Min(0f)] float luzMaxima = 6f;

    [Tooltip("Transparência da casca no auge.")]
    [SerializeField, Range(0f, 1f)] float opacidade = 0.09f;

    static readonly int Base = Shader.PropertyToID("_BaseColor");
    static readonly int Emissao = Shader.PropertyToID("_EmissionColor");

    CicloDoColapso _ciclo;
    MaterialPropertyBlock _bloco;
    Color _corDaCasca = new Color(0.55f, 0.85f, 1f);

    void Awake()
    {
        _ciclo = GetComponent<CicloDoColapso>();
        _bloco = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        float raio = _ciclo.RaioDaOnda;
        float intensidade = _ciclo.Intensidade;
        var fase = _ciclo.Fase;
        bool existe = raio > 0.5f;

        if (casca != null)
        {
            casca.gameObject.SetActive(existe);
            if (existe)
            {
                casca.localScale = Vector3.one * raio * 2f;
                // Mais forte enquanto se move; a casca parada no limite fica discreta.
                float a = opacidade * intensidade * (fase == FaseDoColapso.Fragmentado ? 0.4f : 1f);
                var cor = _corDaCasca;
                cor.a = a;
                _bloco.Clear();
                _bloco.SetColor(Base, cor);
                casca.GetComponent<Renderer>().SetPropertyBlock(_bloco);
            }
        }

        for (int i = 0; i < aneis.Length && i < alturasDosAneis.Length; i++)
        {
            if (aneis[i] == null) continue;
            float h = alturasDosAneis[i];
            bool corta = existe && Mathf.Abs(h) < raio;
            aneis[i].gameObject.SetActive(corta);
            if (!corta) continue;

            float r = Mathf.Sqrt(raio * raio - h * h);
            aneis[i].localPosition = new Vector3(0f, h, 0f);
            aneis[i].localScale = new Vector3(r, 1f + raio * 0.02f, r);
        }

        if (particulas != null)
        {
            var forma = particulas.shape;
            forma.radius = Mathf.Max(0.5f, raio);
            var emissao = particulas.emission;
            emissao.rateOverTime = existe ? Mathf.Lerp(30f, 260f, raio / _ciclo.RaioMaximo) * intensidade : 0f;
            var main = particulas.main;
            main.startSpeed = fase == FaseDoColapso.Retorno || fase == FaseDoColapso.Encerrado ? -9f : 2.5f;
        }

        float carga = _ciclo.Carga * intensidade;
        if (nucleo != null)
        {
            // Pulsa mais rápido conforme carrega: o batimento da ampulheta.
            float pulso = 0.85f + 0.15f * Mathf.Sin(_ciclo.TempoTotal * Mathf.Lerp(2f, 12f, carga));
            _bloco.Clear();
            _bloco.SetColor(Emissao, new Color(1.6f, 1.2f, 0.5f) * (0.25f + carga * 2.2f) * pulso);
            _bloco.SetColor(Base, new Color(1f, 0.85f, 0.5f));
            nucleo.SetPropertyBlock(_bloco);
        }

        if (luz != null) luz.intensity = luzMaxima * carga;
    }
}
