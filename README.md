# Bolao Copa 2026

Aplicacao web em ASP.NET Core Razor Pages para gerenciar o Bolao Premier AEW da Copa do Mundo 2026 entre amigos.

O projeto centraliza palpites por rodada, ranking, regras de pontuacao, estatisticas gerais, cadastro de resultados oficiais e auditoria por comprovante baixavel. A base pode usar SQLite local ou PostgreSQL/Supabase, com autenticacao simples por email/senha.

## Funcionalidades atuais

- Dashboard com ranking, lider atual, estatisticas da Copa e proximos jogos.
- Regulamento do bolao com regras de pontuacao e desempate.
- Cadastro e login por email/senha.
- Palpites por rodada com salvamento temporario associado ao usuario autenticado.
- Palpites especiais com rascunho, finalizacao unica e comprovante baixavel.
- Finalizacao de rodada para travar os palpites.
- Comprovante de auditoria em imagem bloqueado enquanto a rodada nao estiver finalizada.
- Area administrativa protegida por senha para registrar resultado oficial uma unica vez por partida.
- Tabela inicial da Copa 2026 com fase de grupos e cruzamentos do mata-mata por posicao.
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

## Supabase/PostgreSQL

O projeto pode usar SQLite local ou PostgreSQL/Supabase. Por padrao, usa SQLite.

Para Supabase, configure variaveis de ambiente no servidor:

```bash
Database__Provider=Postgres
SUPABASE_DATABASE_URL="postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres"
```

Use `appsettings.Supabase.example.json` apenas como exemplo. Nao commitar senhas reais.

Para aplicar migrations:

```bash
DOTNET_CLI_HOME=.dotnet Database__Provider=Postgres SUPABASE_DATABASE_URL="postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres" dotnet tool run dotnet-ef database update --context BolaoDbContext
```

## Requisitos

- .NET SDK 9.0 ou superior.
