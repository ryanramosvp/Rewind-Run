using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Indicador do rewind: ícone, barra de carga e o comando para usar.
///
/// Fica discreto quando está cheio e sem uso, e aparece por inteiro assim que
/// o jogador gasta ou está gastando carga. O comando escrito acompanha o
/// último aparelho usado - "botão direito" para quem está no mouse, "LB / LT"
/// para quem está no controle.
///
/// Só lê o <see cref="RewindManager"/>; nunca liga, desliga nem recarrega.
/// </summary>
public class RewindHud : MonoBehaviour
{
    [SerializeField] Image chargeFill;
    [SerializeField] CanvasGroup canvasGroup;

    [Tooltip("Ícone do rewind. Opcional; pinta junto com a barra.")]
    [SerializeField] Image icon;
    [Tooltip("Texto com o comando. Opcional.")]
    [SerializeField] TMP_Text hintLabel;

    [Header("Cores")]
    [SerializeField] Color readyColor = new Color(0.36f, 0.78f, 1f);
    [SerializeField] Color activeColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] Color emptyColor = new Color(0.85f, 0.3f, 0.3f);

    [Header("Visibilidade")]
    [SerializeField, Range(0f, 1f)] float idleAlpha = 0.35f;
    [SerializeField, Min(0.1f)] float fadeSpeed = 4f;

    [Header("Comando")]
    [SerializeField] string keyboardHint = "SEGURE  BOTÃO DIREITO";
    [SerializeField] string gamepadHint = "SEGURE  LB / LT";

    bool _gamepad;
    bool _hintWritten;

    void Update()
    {
        var manager = RewindManager.Instance;
        if (manager == null || chargeFill == null) return;

        float charge = manager.ChargeNormalized;
        chargeFill.fillAmount = charge;

        Color color;
        if (manager.IsRewinding) color = activeColor;
        else if (charge <= 0.01f) color = emptyColor;
        else color = readyColor;

        chargeFill.color = color;
        if (icon != null) icon.color = color;

        UpdateHint();

        if (canvasGroup == null) return;

        // Só some quando não há nada a comunicar: carga cheia e rewind parado.
        bool needsAttention = manager.IsRewinding || charge < 0.999f;
        float targetAlpha = needsAttention ? 1f : idleAlpha;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
    }

    void UpdateHint()
    {
        if (hintLabel == null) return;

        // O aparelho mexido por último. O mouse conta só quando anda, que é
        // o que ele faz sem parar enquanto alguém joga com ele.
        double pad = Gamepad.current != null ? Gamepad.current.lastUpdateTime : -1.0;
        double keys = Mathf.Max((float)(Keyboard.current != null ? Keyboard.current.lastUpdateTime : -1.0),
                                (float)(Mouse.current != null ? Mouse.current.lastUpdateTime : -1.0));
        bool gamepad = pad > keys;

        if (_hintWritten && gamepad == _gamepad) return;

        _gamepad = gamepad;
        _hintWritten = true;
        hintLabel.text = gamepad ? gamepadHint : keyboardHint;
    }
}
