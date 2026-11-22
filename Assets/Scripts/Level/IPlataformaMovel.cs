using UnityEngine;

/// <summary>
/// Algo em que a bruxa pode ficar em pé e ser carregada.
///
/// Pergunta "quanto o chão andou neste ponto", e não "quanto a plataforma
/// andou": numa plataforma que só translada as duas coisas são iguais, mas
/// numa que gira (o ponteiro do relógio) o deslocamento depende de onde a
/// bruxa está - perto do eixo quase nada, na ponta muito. O
/// <see cref="PlayerController"/> soma este valor ao próprio movimento.
/// </summary>
public interface IPlataformaMovel
{
    /// <summary>Deslocamento do chão neste quadro, no ponto dado.</summary>
    Vector3 DeslocamentoEm(Vector3 ponto);

    /// <summary>Velocidade do chão no ponto dado, para a bruxa herdar ao saltar.</summary>
    Vector3 VelocidadeEm(Vector3 ponto);
}
