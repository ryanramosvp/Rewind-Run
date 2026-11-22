using UnityEngine;

/// <summary>
/// Plataforma que some e reaparece num ciclo fixo.
///
/// Anda no <see cref="RelogioLocal"/>: dentro de uma zona de âmbar o ciclo
/// inteiro fica mais lento, e a janela em que ela existe dura mais. É o uso
/// mais direto da zona como ferramenta - uma sequência que, fora dela, só um
/// salto perfeito atravessa vira, dentro dela, uma caminhada com calma.
///
/// Avisa antes de sumir: pisca no último trecho do tempo sólido. Sumir sem
/// aviso seria o tipo de obstáculo imprevisível que só se vence decorando.
/// </summary>
[RequireComponent(typeof(RelogioLocal))]
public class PlataformaPiscante : MonoBehaviour
{
    [Tooltip("Segundos sólida, fora de zona.")]
    [SerializeField, Min(0.1f)] float segundosVisivel = 1.6f;

    [Tooltip("Segundos sumida, fora de zona.")]
    [SerializeField, Min(0.1f)] float segundosSumida = 1.2f;

    [Tooltip("Defasagem inicial, de 0 a 1 do ciclo.")]
    [SerializeField, Range(0f, 1f)] float fase;

    [Tooltip("Quanto antes de sumir ela começa a piscar, em segundos do relógio dela.")]
    [SerializeField, Min(0f)] float aviso = 0.45f;

    [SerializeField] GameObject visual;
    [SerializeField] Collider solido;

    RelogioLocal _relogio;
    bool _existe = true;

    void Awake() => _relogio = GetComponent<RelogioLocal>();

    void Update()
    {
        float ciclo = segundosVisivel + segundosSumida;
        float t = Mathf.Repeat(_relogio.Tempo + fase * ciclo, ciclo);

        bool existe = t < segundosVisivel;
        if (existe != _existe)
        {
            _existe = existe;
            if (solido != null) solido.enabled = existe;
        }

        // Pisca a cada 0,1 s do relógio dela: numa zona, o aviso também fica
        // mais lento, e continua dizendo a verdade sobre quanto falta.
        bool piscando = existe && t > segundosVisivel - aviso && Mathf.Repeat(t, 0.2f) < 0.1f;
        if (visual != null) visual.SetActive(existe && !piscando);
    }
}
