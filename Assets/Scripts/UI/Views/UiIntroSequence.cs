using UnityEngine;

/// <summary>
/// Entrada em cascata: cada item desliza um pouco e aparece, um depois do
/// outro.
///
/// Curta de propósito - menos de um segundo do primeiro ao último. É o tempo
/// de o olho seguir a cascata de cima para baixo e terminar na opção já
/// selecionada; mais que isso vira espera para quem só quer jogar. Os botões
/// aceitam clique desde o primeiro quadro.
///
/// Toca de novo cada vez que o objeto é ligado, então voltar de um painel
/// repete a entrada - o que diz que se voltou, sem precisar de texto.
///
/// Os itens devem ser contêineres, não os botões em si: a transparência da
/// entrada fica no CanvasGroup do contêiner, e a do botão desabilitado no
/// dele. Grupos aninhados se multiplicam, então um não apaga o outro.
/// </summary>
public class UiIntroSequence : MonoBehaviour
{
    [Tooltip("Na ordem em que entram.")]
    [SerializeField] RectTransform[] items = new RectTransform[0];

    [Tooltip("De onde cada item vem, relativo ao lugar final.")]
    [SerializeField] Vector2 offset = new Vector2(-60f, 0f);

    [SerializeField, Min(0f)] float startDelay = 0.05f;
    [SerializeField, Min(0f)] float stagger = 0.06f;
    [SerializeField, Min(0.01f)] float duration = 0.4f;

    Vector2[] _home;
    CanvasGroup[] _groups;
    float _clock;
    bool _playing;

    void Awake()
    {
        _home = new Vector2[items.Length];
        _groups = new CanvasGroup[items.Length];

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            _home[i] = items[i].anchoredPosition;

            // A transparência vai num CanvasGroup próprio de cada item, e não
            // na cor: os botões já usam a cor deles para mostrar o estado.
            _groups[i] = items[i].GetComponent<CanvasGroup>();
            if (_groups[i] == null) _groups[i] = items[i].gameObject.AddComponent<CanvasGroup>();
        }
    }

    void OnEnable()
    {
        _clock = 0f;
        _playing = true;
        Step();
    }

    void OnDisable()
    {
        // Desligado no meio da entrada, volta tudo ao lugar: a próxima entrada
        // parte do ponto certo, e quem ler os itens fora daqui os acha inteiros.
        for (int i = 0; i < items.Length; i++) Place(i, 1f);
    }

    void Update()
    {
        if (!_playing) return;

        _clock += Time.unscaledDeltaTime;
        Step();
    }

    void Step()
    {
        bool done = true;

        for (int i = 0; i < items.Length; i++)
        {
            float t = Mathf.Clamp01((_clock - startDelay - i * stagger) / duration);
            if (t < 1f) done = false;
            Place(i, t);
        }

        _playing = !done;
    }

    void Place(int i, float t)
    {
        if (items[i] == null || _home == null) return;

        // Sai rápido e chega devagar: a desaceleração é o que faz o item
        // parecer pousar no lugar em vez de ser parado nele.
        float eased = 1f - Mathf.Pow(1f - t, 3f);

        items[i].anchoredPosition = _home[i] + offset * (1f - eased);
        if (_groups[i] != null) _groups[i].alpha = eased;
    }
}
