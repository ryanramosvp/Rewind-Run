/// <summary>
/// A ordem da campanha: quais fases existem, em que sequência e com que nome.
///
/// O identificador de cada fase é o nome da cena. É o mesmo que o
/// <see cref="GameSession"/> já usava para guardar o recorde quando o campo
/// levelId fica vazio - e fica, em todas as fases -, então os recordes que
/// já estavam no save continuam achando a própria fase. Trocar o id aqui
/// faria a fase "esquecer" o recorde; renomear só o título é seguro.
///
/// O tutorial não entra: não tem cronômetro nem recorde, e é oferecido pelo
/// Novo jogo, não pela seleção de fases.
/// </summary>
public static class Campaign
{
    public readonly struct Level
    {
        public readonly string Id;
        public readonly string Title;

        public Level(string id, string title)
        {
            Id = id;
            Title = title;
        }
    }

    public static readonly Level[] Levels =
    {
        new Level("Level_CorridaDeLava", "Corrida de Lava"),
        new Level("Level_TorreDeBolhas", "Torre de Bolhas"),
        new Level("Level_LavaLetal", "Terra da Lava Letal"),
        new Level("Level_RampaVulcanica", "Rampa Vulcânica"),
    };

    public static string FirstLevel => Levels[0].Id;

    /// <summary>Posição da fase na campanha, ou -1 quando ela não faz parte dela.</summary>
    public static int IndexOf(string levelId)
    {
        for (int i = 0; i < Levels.Length; i++)
        {
            if (Levels[i].Id == levelId) return i;
        }

        return -1;
    }

    public static bool Contains(string levelId) => IndexOf(levelId) >= 0;

    /// <summary>A fase depois desta. Nulo na última e nas que não são da campanha.</summary>
    public static string NextOf(string levelId)
    {
        int index = IndexOf(levelId);
        return index >= 0 && index + 1 < Levels.Length ? Levels[index + 1].Id : null;
    }
}
