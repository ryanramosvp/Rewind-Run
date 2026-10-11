# Rewind Run

Platformer 3D de precisão com foco em speedrun. Vencer é chegar ao fim da fase;
jogar bem é chegar rápido. A personagem pode voltar alguns segundos no tempo
para desfazer um erro — mas os segundos gastos voltando contam no cronômetro
que define a medalha.

Projeto individual da disciplina de Desenvolvimento de Games.
**Unity 6000.5.6f1**, URP, Input System.

## Jogar

| Ação | Tecla |
|---|---|
| Mover | W A S D |
| Pular (e de novo no ar) | Espaço |
| Salto de parede | Espaço junto à parede |
| Voltar no tempo | Q (segurar) |
| Pausar | Esc ou P |

Velocidade é única — não há botão de corrida. Rampas acima de 25° colocam a
personagem em deslize automático.

Há uma mecânica que o jogo não explica: pular no instante exato do pouso
encadeia velocidade. Ela existe de propósito sem indicador na tela.

## Fases

- **Corrida de Lava** — pista suspensa, com prensas, hélices giratórias e uma
  bifurcação entre a rota segura e um salto de 15 m que só o embalo alcança.
- **Torre de Bolhas** — percurso vertical. Dois dos quatro trechos não têm chão:
  a única forma de subir é quicar em bolhas, que devolvem o pulo do ar e somem
  ao serem usadas. O rewind as traz de volta.

## As cenas são geradas por código

Não há cena montada à mão. Tudo — geometria, perigos, HUD, menus — é construído
por scripts em `Assets/Editor/`, acessíveis pelo menu **Rewind Run** no Editor.

```
Rewind Run → Reconstruir Tudo
```

Isso refaz a personagem, o menu e as duas fases na ordem correta. Há ainda a
**Criar Bancada de Testes**, uma cena de desenvolvimento com todas as mecânicas
em sete estações lado a lado, que não entra no build.

A escolha tem uma razão: mudar um número da movimentação e reconstruir as fases
custa um clique. Sem isso, cada ajuste de salto viraria retrabalho manual de
cenário, e o esforço migraria do polimento das mecânicas para o conteúdo.

## Organização

```
Assets/
  Art/        modelos KayKit (cenário e personagem)
  Audio/      efeitos sonoros
  Editor/     geradores de cena, personagem e materiais
  Scenes/     Menu, as duas fases, bancada de testes
  Scripts/    Player, Camera, Level, Rewind, UI
  Settings/   URP e perfil de volume do efeito de rewind
```

## Créditos dos assets

| Asset | Autor | Licença |
|---|---|---|
| KayKit Platformer Pack | Kay Lousberg | CC0 1.0 |
| KayKit Adventurers | Kay Lousberg | CC0 1.0 |
| Interface Sounds | Kenney | CC0 1.0 |
| Fanfarra de vitória, som de pulo | a confirmar | a confirmar |

## Estado

Entrega parcial (P1). Pendências conhecidas: não há música, os tempos de medalha
ainda são estimativas calculadas e não medidas, e a segunda fase só é alcançável
vencendo a primeira — não há seleção de fase no menu.
