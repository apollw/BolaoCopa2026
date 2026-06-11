# AI Context - Bolao Copa 2026

Este documento descreve o estado atual do projeto para que outra IA/agente consiga continuar o desenvolvimento sem depender do historico da conversa.

## Visao Geral

Aplicacao web em ASP.NET Core Razor Pages para gerenciar o Bolao Premier AEW da Copa do Mundo 2026 entre amigos.

O sistema permite cadastro simples por email/senha, registro de palpites por rodada, finalizacao de rodadas, download de comprovante de auditoria em imagem, registro de resultados reais pelo admin, calculo automatico de ranking e exibicao de estatisticas/classificacao.

## Stack

- .NET 9
- ASP.NET Core Razor Pages
- EF Core 9
- PostgreSQL/Supabase
- Autenticacao por cookie
- Bootstrap gerado pelo template + CSS customizado em `wwwroot/css/site.css`

## Como Rodar

Supabase/PostgreSQL:

```bash
DOTNET_CLI_HOME=.dotnet SUPABASE_DATABASE_URL="postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres" dotnet run --project BolaoCopa2026.csproj --urls http://localhost:5086
```

URL local:

```text
http://localhost:5086
```

Encerrar processo local:

```bash
fuser -k 5086/tcp
```

## Banco de Dados

O projeto usa exclusivamente PostgreSQL/Supabase.

Para Supabase/PostgreSQL, usar:

```bash
SUPABASE_DATABASE_URL="postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres"
```

Usar preferencialmente a porta `5432` do pooler de sessao do Supabase. A porta `6543` apresentou timeout em DDL/migrations durante o setup:

```bash
SUPABASE_DATABASE_URL="postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres"
```

O `Program.cs` aceita `SUPABASE_DATABASE_URL` ou `DATABASE_URL` no formato URL do Supabase e converte internamente para connection string Npgsql. O projeto contem `appsettings.Supabase.example.json` sem segredo real.

`Services/BolaoSeedData.cs` aplica migrations com `Database.Migrate()` e depois cria apenas dados-base da Copa.

## Deploy

Vercel nao e a melhor opcao para este projeto porque a aplicacao e ASP.NET Core Razor Pages com servidor Kestrel persistente. O projeto foi preparado para Render com Docker:

- `Dockerfile` publica o app em Release e roda `BolaoCopa2026.dll`;
- `render.yaml` cria web service free;
- `SUPABASE_DATABASE_URL` deve ser configurada como segredo no painel do Render.
- `Admin__Password` deve ser configurada no painel do Render para habilitar o acesso admin.
- Render gera URL publica `*.onrender.com`; no plano free pode dormir e demorar no primeiro acesso.

## Arquivos Principais

- `Program.cs`: registra Razor Pages, EF Core PostgreSQL/Supabase, autenticacao por cookie, policies de autorizacao, `BolaoRepository`, `ScoringService` e executa migrations/seed.
- `Data/BolaoDbContext.cs`: mapeamento EF Core das entidades.
- `Models/BolaoModels.cs`: modelos de dominio e view models.
- `Services/BolaoRepository.cs`: principal camada de acesso/regras de aplicacao.
- `Services/BolaoSeedData.cs`: seed inicial apenas de rodadas e jogos da Copa.
- `Services/ScoringService.cs`: regra de pontuacao.
- `Services/AuditImageService.cs`: gera comprovante de auditoria em SVG com metadados e hash.
- `Pages/Palpites/Index.*`: tela de palpites por rodada.
- `Pages/Palpites/Especiais.*`: tela de palpites especiais.
- `Pages/Admin/Login.*`: login separado do organizador/admin.
- `Pages/Admin/Resultados.*`: registro de resultados reais protegido por policy `AdminOnly`.
- `Pages/Conta/Cadastro.*`, `Login.*`, `Logout.*`, `Perfil.*`: fluxo simples de conta.
- `Pages/Index.*`: dashboard principal.
- `wwwroot/css/site.css`: estilos principais.
- `Dockerfile`, `.dockerignore`, `render.yaml`: deploy Docker no Render.

## Entidades e Conceitos

### Participant

Representa usuario/participante.

Campos relevantes:

- `Id`
- `Name`
- `Email`
- `Login`
- `PasswordHash`
- `IsAdmin`

O login de participante usa email/senha e cookie. A area admin exige sessao com role `Admin`, criada em `/Admin/Login` pela senha administrativa.

A senha admin nao possui mais valor padrao em codigo.
Ela deve ser configurada por `Admin__Password`.

### PredictionRound

Rodada de preenchimento do bolao:

- Rodada 1 da fase de grupos
- Rodada 2 da fase de grupos
- Rodada 3 da fase de grupos
- Rodada de 32
- Oitavas
- Quartas
- Semifinais
- Terceiro lugar e final

### Match

Partida da Copa.

Campos relevantes:

- `OfficialNumber`: numero oficial/ordenacao do jogo.
- `RoundId`: rodada de preenchimento.
- `HomeTeam` e `AwayTeam`: owned type `Team`.
- `Phase`
- `Kickoff`
- `GroupName`
- `Venue`
- `Result`: owned type `MatchResult`, preenchido pelo admin.

### Prediction

Palpite de participante para uma partida.

Chave composta:

- `ParticipantId`
- `MatchId`

Campos:

- `HomeGoals`
- `AwayGoals`
- `QualifiedTeamCode`
- `SavedAt`
- `SubmittedAt`

Enquanto `SubmittedAt == null`, e rascunho. Quando a rodada e finalizada, `SubmittedAt` recebe data/hora e o palpite fica travado.

### RoundSubmission

Registro persistente de fechamento de uma rodada por participante.

Campos:

- `ParticipantId`
- `RoundId`
- `SubmittedAt`
- `AuditDownloadedAt`
- `AuditProofHash`

Esse registro e criado em `BolaoRepository.FinalizeRound(...)`. Ele e a fonte principal para saber se uma rodada foi finalizada pelo participante. Os `Predictions.SubmittedAt` continuam existindo como trava individual dos palpites.

### ResultAudit

Registro de auditoria de resultado real cadastrado pelo admin.

Nao confundir com o comprovante de auditoria baixado pelo participante.

### SpecialPrediction

Palpites especiais do participante.

Chave:

- `ParticipantId`

Campos:

- `Champion`
- `RunnerUp`
- `TopScorer`
- `GoldenBall`
- `SavedAt`
- `SubmittedAt`
- `AuditDownloadedAt`
- `AuditProofHash`

Enquanto `SubmittedAt == null`, e rascunho. Quando finalizado, fica bloqueado definitivamente.

## Regras de Pontuacao

Implementadas em `Services/ScoringService.cs`.

Fase de grupos:

- Placar exato: 5 pontos.
- Resultado da partida: 3 pontos.

Jogos do Brasil:

- Pontuacao dobrada em qualquer fase.

Mata-mata:

- Considera resultado ao fim da prorrogacao.
- Placar exato: 5 pontos.
- Acerto do resultado: 3 pontos.
- Acerto do classificado: 2 pontos.
- Maximo por partida: 7 pontos antes da dobra do Brasil.

Ranking:

- Calculado sob demanda em `BolaoRepository.GetRanking()`.
- Usa apenas palpites finalizados (`SubmittedAt != null`).
- Compara `Prediction` com `Match.Result`.

Desempate aplicado no ordering:

1. Pontos
2. Placares exatos
3. Acertos de classificado no mata-mata
4. Acertos em jogos do Brasil
5. Acertos de resultado
6. Nome

## Fluxo de Palpites

Pagina: `/Palpites`

O usuario pode:

- escolher rodada;
- salvar rascunhos;
- finalizar rodada;
- baixar comprovante de auditoria quando a rodada estiver finalizada.

Regras de bloqueio:

- Rodada 1 sempre liberada.
- Rodada 2 so libera quando o participante finaliza a rodada 1.
- Rodada 3 so libera quando o participante finaliza a rodada 2.
- Rodada de 32 so libera quando todos os resultados reais da fase de grupos forem registrados pelo admin.
- Fases seguintes do mata-mata liberam quando a fase anterior tiver todos os resultados reais registrados.

Essas regras existem tanto na UI quanto no backend:

- `BolaoRepository.IsRoundAvailable(...)`

## Fluxo de Palpites Especiais

Pagina: `/Palpites/Especiais`

O participante pode salvar rascunho de campeao, vice-campeao, artilheiro e Bola de Ouro ate finalizar. Ao finalizar, `SpecialPrediction.SubmittedAt` e preenchido e os campos ficam bloqueados definitivamente. O comprovante SVG fica disponivel apenas depois da finalizacao.

Regras principais:

- rascunho pode ser editado enquanto `SubmittedAt` estiver nulo;
- finalizacao e unica, sem edicao posterior;
- especiais tambem bloqueiam apos o inicio da primeira partida da Copa;
- comprovante grava hash em `SpecialPrediction.AuditProofHash` e horario em `AuditDownloadedAt`.

Metodos principais:

- `SaveSpecialPredictionDraft(...)`
- `FinalizeSpecialPrediction(...)`
- `MarkSpecialAuditDownloaded(...)`

## Resultados Reais

Pagina: `/Admin/Resultados`

O admin registra resultado real de uma partida uma unica vez. Depois que `Match.Result` existe, a partida fica bloqueada contra edicao.

Para mata-mata, o admin tambem precisa informar `QualifiedTeamCode`.

Quando um resultado real e registrado:

- o resultado fica em `Match.Result`;
- um `ResultAudit` e criado;
- ranking passa a considerar aquele jogo automaticamente.

## Classificacao de Grupos

Dashboard principal mostra classificacao por grupo.

Implementacao:

- `BolaoRepository.GetGroupStandings()`
- `BuildGroupEntries(...)`

Calcula:

- jogos
- vitorias
- empates
- derrotas
- gols pro
- gols contra
- saldo
- pontos

Ordenacao:

1. Pontos
2. Saldo de gols
3. Gols pro
4. Nome

Ainda nao implementa todos os criterios oficiais FIFA de desempate. Isso deve ser melhorado antes de producao.

## Mata-Mata

Seed inclui placeholders da rodada de 32, oitavas, quartas, semifinais, terceiro lugar e final.

Exemplos:

- `2A x 2B`
- `1E x 3A/B/C/D/F`
- `W73 x W75`

Depois que todos os resultados da fase de grupos estiverem registrados, `BolaoRepository.TryApplyKnockoutPairings()` tenta preencher a rodada de 32 com:

- primeiros colocados;
- segundos colocados;
- melhores terceiros.

Limite atual importante:

- A logica de melhores terceiros e simplificada. Ela escolhe os melhores terceiros elegiveis conforme os grupos permitidos no placeholder, mas ainda nao implementa integralmente a tabela oficial FIFA de combinacoes de terceiros.
- Oitavas em diante ainda ficam como placeholders `W73`, `W75`, etc. Elas devem ser preenchidas automaticamente apos resultados da fase anterior.

## Seed da Copa 2026

`Services/BolaoSeedData.cs` cria:

- 8 rodadas de preenchimento;
- 72 jogos da fase de grupos;
- placeholders do mata-mata.

Nao cria usuarios, participantes demo nem palpites especiais demo.

Banco existente:

- Se `Rounds` ja existir, o seed nao recria tudo.
- Ele chama `EnsureMissingMatches(db)` para acrescentar partidas faltantes sem apagar dados.

Observacao importante:

- Para rodadas 2 e 3 da fase de grupos, onde a seed nao tem todos os horarios/sedes oficiais confirmados, usa `A definir` e horarios padrao.
- Se for necessario 100% de fidelidade de agenda, pesquisar e substituir esses dados.

## Autenticacao

Fluxo simples por email/senha:

- `/Conta/Cadastro`
- `/Conta/Login`
- `/Conta/Logout`
- `/Conta/Perfil`

Implementacao:

- Cookie auth em `Program.cs`.
- `PasswordHasher<Participant>` para hash de senha.
- Claims:
  - `ClaimTypes.NameIdentifier`: `Participant.Id`
  - `ClaimTypes.Name`
  - `ClaimTypes.Email`
  - `ClaimTypes.Role`: `Participante`

`BolaoRepository.CurrentParticipantId` tenta ler o claim do usuario autenticado. Se nao houver identificador de participante, dispara excecao em vez de usar fallback.

Admin:

- `/Admin/Login` valida a senha do organizador e cria uma sessao cookie com role `Admin`.
- `/Admin/Resultados` usa `[Authorize(Policy = "AdminOnly")]`.
- `/Palpites` e `/Conta/Perfil` usam `ParticipantOnly`, entao admin nao entra como participante.

## Auditoria

O email do participante e salvo em `Participant.Email` e usado como identidade/login. Nao ha envio de email.

A auditoria e feita por download de imagem SVG gerada pela aplicacao.

Implementacao:

- `Services/AuditImageService.cs`
- `Pages/Palpites/Index.cshtml.cs`, handler `OnGetDownloadAudit`
- `Pages/Palpites/Especiais.cshtml.cs`, handler `OnGetDownloadAudit`

O SVG inclui:

- participante;
- email;
- rodada;
- data de geracao em horario de Brasilia;
- lista de palpites finalizados;
- horario de salvamento;
- horario de finalizacao;
- hash SHA-256 do conteudo canonico do comprovante.

O SVG grava metadados com `timezone=America/Sao_Paulo` e `generatedAtBrasilia`. O banco continua usando UTC internamente para compatibilidade correta com PostgreSQL `timestamp with time zone`.

O botao so fica disponivel quando a rodada esta finalizada. Se a rodada ainda estiver em rascunho ou bloqueada, o download nao e gerado.

Proximo passo:

- se quiser PNG em vez de SVG, renderizar o SVG no browser e converter para PNG, ou adicionar uma biblioteca server-side de renderizacao.
- criar uma tela para listar comprovantes ja gerados/baixados usando `RoundSubmission`.

## Problemas Resolvidos

### Footer no meio da pagina

Causa: `Pages/Shared/_Layout.cshtml.css` sobrescrevia o `site.css` com `.footer { position: absolute; }`.

Correcao: regra removida e layout principal usa `body` flex column.

## Pendencias Importantes

1. Criar tela administrativa/participante para consultar `RoundSubmission`.
2. Completar horarios/sedes oficiais das rodadas 2 e 3, se necessario.
3. Implementar criterios oficiais completos de desempate de grupos.
4. Completar propagacao automatica do mata-mata apos cada fase.
5. Testar fluxo completo com multiplos usuarios em producao.

## Comandos Uteis

Build:

```bash
DOTNET_CLI_HOME=.dotnet dotnet build BolaoCopa2026.csproj
```

Run:

```bash
DOTNET_CLI_HOME=.dotnet SUPABASE_DATABASE_URL="postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres" dotnet run --project BolaoCopa2026.csproj --urls http://localhost:5086
```

Run com Supabase:

```bash
DOTNET_CLI_HOME=.dotnet SUPABASE_DATABASE_URL="postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres" dotnet run --project BolaoCopa2026.csproj --urls http://localhost:5086
```

Migrations no Supabase:

```bash
DOTNET_CLI_HOME=.dotnet SUPABASE_DATABASE_URL="postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres" dotnet tool run dotnet-ef database update --context BolaoDbContext
```

Encerrar app preso na porta 5086:

```bash
fuser -k 5086/tcp
```
