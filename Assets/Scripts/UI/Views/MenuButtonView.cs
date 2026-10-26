using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A parte visual de um controle de menu - botão, linha de opção ou barra:
/// placa de pedra, moldura de metal, brilho de brasa e um losango que marca a
/// opção escolhida.
///
/// Só aparência. O que o controle FAZ continua no onClick do Button (ou no
/// evento da barra), ligado ao <see cref="MainMenu"/> e companhia - este
/// componente nunca chama lógica de jogo.
///
/// Passar o mouse por cima seleciona o botão, em vez de criar um estado
/// "hover" separado. Com dois destaques possíveis (o do mouse e o do teclado)
/// a tela mostraria duas opções acesas ao mesmo tempo, e o Enter iria para a
/// que o jogador não está olhando. Assim existe uma escolhida só, venha ela de
/// onde vier.
///
/// Anima com tempo sem escala: o menu de pausa roda com o jogo parado.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class MenuButtonView : MonoBehaviour,
    IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [SerializeField] UiTheme theme;

    [Header("Partes")]
    [SerializeField] Image plate;
    [SerializeField] Image frame;
    [SerializeField] Image glow;
    [SerializeField] RectTransform marker;
    [SerializeField] TMP_Text label;
    [Tooltip("Linha pequena embaixo do rótulo. Opcional.")]
    [SerializeField] TMP_Text detail;
    [SerializeField] CanvasGroup group;
    [Tooltip("O que cresce e afunda. Vazio é o próprio objeto; numa barra é a linha inteira, não só o trilho.")]
    [SerializeField] Transform scaleTarget;

    [Header("Ajustes")]
    [Tooltip("Quanto o rótulo anda para a direita ao ser escolhido, em px.")]
    [SerializeField] float labelShift = 14f;
    [SerializeField, Range(0f, 1f)] float disabledAlpha = 0.38f;

    Selectable _button;
    RectTransform _labelRect;
    Vector2 _labelHome;
    bool _selected;
    bool _pressed;
    float _focus;      // 0 parado, 1 escolhido
    float _press;      // 0 solto, 1 apertado
    float _punch;      // pulso do clique, decai sozinho

    void Awake()
    {
        _button = GetComponent<Selectable>();
        if (scaleTarget == null) scaleTarget = transform;

        // A transição do Selectable pintaria por cima das cores daqui.
        _button.transition = Selectable.Transition.None;
        if (_button is Button button) button.onClick.AddListener(() => _punch = 1f);

        if (label != null)
        {
            _labelRect = label.rectTransform;
            _labelHome = _labelRect.anchoredPosition;
        }

        Apply(instant: true);
    }

    void OnEnable()
    {
        // Ao voltar de um painel o botão pode reaparecer já selecionado, sem
        // receber OnSelect de novo.
        var system = EventSystem.current;
        _selected = system != null && system.currentSelectedGameObject == gameObject;
        _pressed = false;
        Apply(instant: true);
    }

    void Update() => Apply(instant: false);

    void Apply(bool instant)
    {
        if (theme == null) return;

        bool interactable = _button == null || _button.IsInteractable();
        float dt = Time.unscaledDeltaTime;
        float k = instant ? 1f : 1f - Mathf.Exp(-theme.transitionSpeed * dt);

        _focus = Mathf.Lerp(_focus, interactable && _selected ? 1f : 0f, k);
        _press = Mathf.Lerp(_press, interactable && _pressed ? 1f : 0f, instant ? 1f : 1f - Mathf.Exp(-40f * dt));
        _punch = instant ? 0f : Mathf.MoveTowards(_punch, 0f, dt * 5f);

        float scale = Mathf.Lerp(1f, theme.selectedScale, _focus)
                      * Mathf.Lerp(1f, theme.pressedScale, _press)
                      * (1f + 0.05f * Mathf.Sin(_punch * Mathf.PI) * _punch);
        if (scaleTarget != null) scaleTarget.localScale = new Vector3(scale, scale, 1f);

        if (plate != null) plate.color = Color.Lerp(theme.stone, theme.stoneLit, _focus);
        if (frame != null) frame.color = Color.Lerp(theme.metal, theme.gold, _focus);

        if (glow != null)
        {
            var color = Color.Lerp(theme.ember, theme.molten, _punch);
            color.a = _focus * 0.85f + _punch * 0.15f;
            glow.color = color;
        }

        if (marker != null) marker.localScale = Vector3.one * _focus;

        if (label != null)
        {
            label.color = Color.Lerp(theme.sand, theme.highlightText, _focus);
            if (_labelRect != null) _labelRect.anchoredPosition = _labelHome + Vector2.right * (labelShift * _focus);
        }

        if (detail != null) detail.color = Color.Lerp(theme.ash, theme.gold, _focus);

        if (group != null)
        {
            float alpha = interactable ? 1f : disabledAlpha;
            group.alpha = instant ? alpha : Mathf.MoveTowards(group.alpha, alpha, dt * 4f);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button == null || !_button.IsInteractable()) return;

        var system = EventSystem.current;
        if (system != null && system.currentSelectedGameObject != gameObject)
            system.SetSelectedGameObject(gameObject, eventData);
    }

    public void OnPointerExit(PointerEventData eventData) => _pressed = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) _pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData) => _pressed = false;

    public void OnSelect(BaseEventData eventData) => _selected = true;

    public void OnDeselect(BaseEventData eventData)
    {
        _selected = false;
        _pressed = false;
    }

    // Enter e o botão do controle não passam pelo ponteiro: o pulso do clique
    // vem do onClick, que os dois disparam.
    public void OnSubmit(BaseEventData eventData) { }
}
