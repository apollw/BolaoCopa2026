# Bolao Copa 2026

Aplicacao web em ASP.NET Core Razor Pages para gerenciar o Bolao Premier AEW da Copa do Mundo 2026 entre amigos.

O projeto centraliza palpites por rodada, ranking, regras de pontuacao, estatisticas gerais, cadastro de resultados oficiais e auditoria. A base atual usa dados em memoria com um usuario mockado, servindo como estrutura inicial para evoluir para banco de dados, autenticacao real e envio de email.

## Funcionalidades atuais

- Dashboard com ranking, lider atual, estatisticas da Copa e proximos jogos.
- Regulamento do bolao com regras de pontuacao e desempate.
- Palpites por rodada com salvamento temporario no perfil do usuario mockado.
- Finalizacao de rodada para travar os palpites.
- Auditoria por email bloqueada enquanto a rodada nao estiver finalizada.
- Area administrativa para registrar resultado oficial uma unica vez por partida.
- Tabela inicial da Copa 2026 com primeira rodada da fase de grupos e cruzamentos oficiais do mata-mata por posicao.
- Interface responsiva para dispositivos moveis.

## Como rodar

Na raiz do projeto, execute:

```bash
DOTNET_CLI_HOME=.dotnet dotnet run --project BolaoCopa2026.csproj --urls http://localhost:5086
```

Depois acesse:

```text
http://localhost:5086
```

## Requisitos

- .NET SDK 9.0 ou superior.
