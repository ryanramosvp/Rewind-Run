using UnityEngine;

/// <summary>
/// Preferências do jogador, guardadas entre sessões.
///
/// Usa PlayerPrefs porque são duas ou três opções soltas. Os recordes, que são
/// dado estruturado, ficam no arquivo do <see cref="SaveSystem"/>.
/// </summary>
public static class GameSettings
{
    const string kShowTimer = "rr.showTimer";
    const string kShowSpeed = "rr.showSpeed";
    const string kVolume = "rr.volume";

    /// <summary>Cronômetro visível durante a fase.</summary>
    public static bool ShowTimer
    {
        get => PlayerPrefs.GetInt(kShowTimer, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(kShowTimer, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>Medidor de velocidade. Interessa a quem persegue tempo, atrapalha quem não.</summary>
    public static bool ShowSpeed
    {
        // Desligada por padrão: ela denuncia o embalo do bunny hop, que é para
        // ser descoberto jogando. Quem quiser o medidor liga nas opções.
        get => PlayerPrefs.GetInt(kShowSpeed, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(kShowSpeed, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Volume geral, de 0 a 1.
    ///
    /// Mexe no AudioListener.volume, que é um multiplicador global: assim vale
    /// para tudo de uma vez - cliques, fanfarra, pulo - sem que cada tocador
    /// precise consultar a preferência. Um AudioMixer daria canais separados
    /// para música e efeitos, mas enquanto houver um controle só, ele seria
    /// estrutura sem uso.
    /// </summary>
    public static float Volume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(kVolume, 0.8f));
        set
        {
            float clamped = Mathf.Clamp01(value);

            PlayerPrefs.SetFloat(kVolume, clamped);
            PlayerPrefs.Save();

            AudioListener.volume = clamped;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Devolve ao motor de áudio o volume guardado.
    ///
    /// Precisa ser chamado a cada cena: AudioListener.volume é estado do
    /// processo, e entrar em Play no Editor o devolve a 1.
    /// </summary>
    public static void ApplyVolume() => AudioListener.volume = Volume;

    /// <summary>Avisa quem estiver na tela para se redesenhar na hora.</summary>
    public static event System.Action Changed;
}
