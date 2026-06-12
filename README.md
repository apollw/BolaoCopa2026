# Bolao Copa 2026

Aplicacao web em ASP.NET Core Razor Pages para gerenciar o Bolao Premier AEW da Copa do Mundo 2026 entre amigos.

O projeto centraliza palpites por rodada, ranking, regras de pontuacao, estatisticas gerais, cadastro de resultados oficiais e auditoria por comprovante baixavel. A aplicacao usa PostgreSQL/Supabase em desenvolvimento e producao, com autenticacao simples por email/senha.

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
- Deploy Docker preparado para Render.

## Como rodar

Local com Supabase/PostgreSQL:

```bash
DOTNET_CLI_HOME=.dotnet SUPABASE_DATABASE_URL="postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres" dotnet run --project BolaoCopa2026.csproj --urls http://localhost:5086
```

Acesse:

```text
http://localhost:5086
```

Para encerrar a aplicacao local na porta 5086:

```bash
fuser -k 5086/tcp
```

## Acessos

Usuario normal:

```text
/Conta/Cadastro
/Conta/Login
/Palpites
/Palpites/Especiais
/Conta/Perfil
```

Admin:

```text
/Admin/Login
```

A senha administrativa deve ser configurada por `Admin__Password` no ambiente antes de usar `/Admin/Login`.

## Supabase/PostgreSQL

O projeto usa exclusivamente PostgreSQL/Supabase em desenvolvimento e producao.

Para Supabase, configure variaveis de ambiente no servidor:

```bash
SUPABASE_DATABASE_URL="postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres"
```

Use `appsettings.Supabase.example.json` apenas como exemplo. Nao commitar senhas reais.

Em PostgreSQL/Supabase, o app roda migrations automaticamente na inicializacao com `Database.Migrate()` e depois cria apenas dados-base da Copa, sem usuarios mockados.

Para aplicar migrations:

```bash
DOTNET_CLI_HOME=.dotnet SUPABASE_DATABASE_URL="postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres" dotnet tool run dotnet-ef database update --context BolaoDbContext
```

## Deploy Online Rapido

O projeto esta preparado para deploy Docker no Render usando `Dockerfile` e `render.yaml`.

Passos:

1. Suba este repositorio para o GitHub.
2. No Render, crie um novo Web Service a partir do repositorio.
3. Escolha Docker/free instance.
4. Configure a variavel secreta `SUPABASE_DATABASE_URL` com a URL real do Supabase.
5. Confirme o deploy.

As demais variaveis ficam em `render.yaml`: `ASPNETCORE_URLS=http://0.0.0.0:10000`.

Variaveis recomendadas no Render:

```text
SUPABASE_DATABASE_URL=postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres
Admin__Password=uma-senha-de-admin
```

O Render vai gerar uma URL publica do tipo `https://nome-do-servico.onrender.com`.

## Requisitos

- .NET SDK 9.0 ou superior.
