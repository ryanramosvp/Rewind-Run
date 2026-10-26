using TMPro;
using UnityEngine;

/// <summary>
/// Pulso curto num elemento: cresce, volta, e o texto pisca numa cor.
///
/// Chamado de fora, por quem sabe que algo mudou - o HUD avisa quando o
/// contador de mortes sobe. O componente só sabe se mexer; não sabe por quê.
///
/// Tempo sem escala: a tela de morte para o jogo, e o pulso tem que tocar
/// quando ela sai, não ficar congelado no meio.
/// </summary>
public class UiPulse : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] Color flashColor = new Color(0.90f, 0.27f, 0.20f);
    [SerializeField, Min(0f)] float scale = 0.25f;
    [SerializeField, Min(0.05f)] float duration = 0.45f;

    float _t = 1f;
    Color _rest;
    bool _hasRest;

    public void Play()
    {
        if (text != null && !_hasRest)
        {
            _rest = text.color;
            _hasRest = true;
        }

        _t = 0f;
    }

    void Update()
    {
        if (_t >= 1f) return;

        _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / duration);

        // Sobe de uma vez e assenta devagar.
        float bump = Mathf.Sin(Mathf.Min(1f, _t * 2.2f) * Mathf.PI) * (1f - _t);
        transform.localScale = Vector3.one * (1f + scale * bump);

        if (text != null && _hasRest) text.color = Color.Lerp(flashColor, _rest, _t * _t);
    }
}
