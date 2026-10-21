using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Conduz o tutorial: um comando por vez, e só passa quando o jogador o faz.
///
/// Esperar a ação, em vez de mostrar os textos em sequência por tempo, é o
/// ponto. Ler "aperte Espaço no ar" não ensina o pulo duplo; fazê-lo ensina. E
/// quem já sabe passa por cada passo em um segundo, sem ficar esperando um
/// texto terminar.
///
/// A ordem segue a dependência entre os comandos: andar, pular, pulo duplo,
/// parede, e só então o rewind - que precisa de história gravada e, mais
/// importante, faz mais sentido depois que o jogador já errou alguma coisa.
/// A subida da espiral fecha usando tudo junto.
///
/// Morrer não volta passos. A queda já é a lição, e repetir o que se acabou de
/// provar que sabe fazer seria punição.
/// </summary>
public class TutorialGuide : MonoBehaviour
{
    enum Goal { Andar, Pular, PuloDuplo, SaltoDeParede, VoltarNoTempo, ChegarAoTopo }

    static readonly (Goal goal, string text)[] Steps =
    {
        (Goal.Andar, "Use WASD para andar."),
        (Goal.Pular, "Aperte ESPAÇO para pular."),
        (Goal.PuloDuplo, "Pule e, ainda no ar, aperte ESPAÇO de novo: é o pulo duplo."),
        (Goal.SaltoDeParede, "Pule contra a parede do vulcão e, encostada nela, " +
                             "aperte ESPAÇO: é o salto de parede."),
        (Goal.VoltarNoTempo, "Segure o BOTÃO DIREITO do mouse para voltar no tempo. " +
                             "A barra embaixo mostra quanto ainda resta."),
        (Goal.ChegarAoTopo, "Agora suba pela escada em espiral até a bandeira no topo do vulcão."),
    };

    [SerializeField] PlayerController player;
    [SerializeField] Text titleLabel;
    [SerializeField] Text bodyLabel;

    [Tooltip("Fase carregada ao terminar o tutorial.")]
    [SerializeField] string nextScene = "Level_CorridaDeLava";

    [Header("Metas")]
    [Tooltip("Metros andados no chão para o passo de andar valer. Curto de " +
             "propósito: da largada até a parede da torre são só 4 m, e quem " +
             "segura W em linha reta não pode empacar no primeiro passo.")]
    [SerializeField, Min(0.5f)] float walkDistance = 3f;

    [Tooltip("Segundos de rewind para o passo valer. Um toque rápido não chega " +
             "a mostrar o tempo voltando.")]
    [SerializeField, Min(0.1f)] float rewindSeconds = 0.6f;

    [Tooltip("Centro do topo do vulcão e o meio-tamanho da área que conta como chegada.")]
    [SerializeField] Vector3 summit = new Vector3(0f, 15f, 0f);
    [SerializeField] Vector3 summitHalfSize = new Vector3(3.2f, 2f, 3.2f);

    [Header("Ritmo")]
    [Tooltip("Quanto o \"BOA!\" fica na tela antes do próximo passo.")]
    [SerializeField, Min(0f)] float praiseSeconds = 1f;

    [Tooltip("Espera entre o fim do tutorial e o carregamento da fase.")]
    [SerializeField, Min(0f)] float finishSeconds = 2.5f;

    int _index;
    float _progress;
    float _praiseTimer = -1f;
    float _finishTimer = -1f;
    Vector3 _lastPosition;

    Goal Current => Steps[_index].goal;

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            enabled = false;
            return;
        }

        player.Jumped += OnJumped;
        player.WallJumped += OnWallJumped;
        _lastPosition = player.transform.position;

        Show();
    }

    void OnDestroy()
    {
        if (player == null) return;

        player.Jumped -= OnJumped;
        player.WallJumped -= OnWallJumped;
    }

    void Update()
    {
        // Tempo escalado de propósito: com a pausa ou a tela de morte no ar o
        // tutorial congela junto, em vez de avançar com ninguém olhando.
        float dt = Time.deltaTime;

        if (_finishTimer >= 0f)
        {
            _finishTimer -= dt;
            if (_finishTimer < 0f) SceneManager.LoadScene(nextScene);
            return;
        }

        if (_praiseTimer >= 0f)
        {
            _praiseTimer -= dt;
            if (_praiseTimer < 0f) Advance();
            return;
        }

        Vector3 position = player.transform.position;

        switch (Current)
        {
            case Goal.Andar:
                Vector3 step = position - _lastPosition;
                step.y = 0f;

                // Conta também no ar. A bruxa nasce um metro acima do chão, e
                // quem já entra segurando W chega à parede da torre antes de
                // pousar - contando só no chão, o passo nunca fechava.
                // O teto por quadro ignora o teleporte do respawn, que de outro
                // modo contaria como uma caminhada de vinte metros.
                _progress += Mathf.Min(step.magnitude, 1f);
                if (_progress >= walkDistance) Complete();
                break;

            case Goal.VoltarNoTempo:
                var rewind = RewindManager.Instance;
                if (rewind != null && rewind.IsRewinding) _progress += dt;
                if (_progress >= rewindSeconds) Complete();
                break;

            case Goal.ChegarAoTopo:
                Vector3 offset = position - summit;
                if (Mathf.Abs(offset.x) <= summitHalfSize.x
                    && Mathf.Abs(offset.y) <= summitHalfSize.y
                    && Mathf.Abs(offset.z) <= summitHalfSize.z)
                    Complete();
                break;
        }

        _lastPosition = position;
    }

    void OnJumped(bool isAirJump)
    {
        if (_praiseTimer >= 0f || _finishTimer >= 0f) return;

        if (Current == Goal.Pular && !isAirJump) Complete();
        else if (Current == Goal.PuloDuplo && isAirJump) Complete();
    }

    void OnWallJumped()
    {
        if (_praiseTimer >= 0f || _finishTimer >= 0f) return;

        if (Current == Goal.SaltoDeParede) Complete();
    }

    void Complete()
    {
        UiSounds.Confirm();
        SetText("BOA!", bodyLabel != null ? bodyLabel.text : string.Empty);
        _praiseTimer = praiseSeconds;
    }

    void Advance()
    {
        _index++;
        _progress = 0f;

        if (_index < Steps.Length)
        {
            Show();
            return;
        }

        // O índice fica no último passo: Current continua válido, e o Update
        // já não chega a consultá-lo porque o fim tem prioridade.
        _index = Steps.Length - 1;

        UiSounds.Victory();
        SetText("TUTORIAL CONCLUÍDO", "Agora é para valer: indo para a primeira fase.");
        _finishTimer = finishSeconds;
    }

    void Show() =>
        SetText($"TUTORIAL  ·  {_index + 1} DE {Steps.Length}", Steps[_index].text);

    void SetText(string title, string body)
    {
        if (titleLabel != null) titleLabel.text = title;
        if (bodyLabel != null) bodyLabel.text = body;
    }
}
