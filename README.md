# ZulfarakRPG

Idle RPG online inspirado no TaskbarHero, ambientado na cidade desértica de Zulfarak (temática Qatar).

## Como abrir no Unity

1. Instale **Unity Hub** + Unity **2022 LTS** (ou 6000 LTS)
2. Abra o Unity Hub → Add → selecione a pasta `ZulfarakRPG`
3. O Unity importará os pacotes automaticamente (Packages/manifest.json)
4. Em caso de erro com Steamworks.NET: instale pelo Package Manager via Git URL:
   `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net`

## Rodar o servidor

```bash
cd Server
npm install
npm start
```

O servidor WebSocket sobe em `ws://localhost:3000`.

## Estrutura de Cenas

| Cena | Descrição |
|---|---|
| `Bootstrap` | Cena de boot — inicializa managers, decide rota |
| `CharacterCreation` | Criação de personagem (aparece só na primeira vez) |
| `Zulfarak` | Cidade principal — hub do jogo |

## Classes e Subclasses

| Classe | Subclasse | Papel |
|---|---|---|
| Mago | Clérigo | Healer |
| Mago | Fogo | DPS |
| Mago | Gelo | DPS |
| Mago | Raio | DPS |
| Guerreiro | Escudeiro | Tank |
| Guerreiro | Lanceiro | DPS |
| Guerreiro | Berserker | DPS |
| Arqueiro | Sobrevivência | Healer |
| Arqueiro | Caçador | DPS |
| Arqueiro | Rastreador | DPS |

## Missões iniciais

- **Individual**: "Patrulha do Deserto" — missão solo contra bandidos
- **Guilda**: "A Masmorra de Zulfarak" — requer 5 jogadores online e prontos

## Sistema de Guilda

- Máximo de 5 membros
- Conta é vinculada ao Steam (sem login separado)
- Missões de guilda exigem todos os membros online e prontos na sala de lobby
