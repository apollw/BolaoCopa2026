# Bolao Copa 2026

Aplicacao web em ASP.NET Core Razor Pages para gerenciar o Bolao Premier AEW da Copa do Mundo 2026 entre amigos.

O projeto centraliza palpites por rodada, ranking, regras de pontuacao, estatisticas gerais, cadastro de resultados oficiais e auditoria por comprovante baixavel. A aplicacao usa PostgreSQL/Supabase em desenvolvimento e producao, com autenticacao simples por email/senha.

## Funcionalidades atuais

- Dashboard com ranking, lider atual, estatisticas da Copa e proximos jogos.
- Ranking do painel com destaque visual para posicoes.
- Regulamento do bolao com regras de pontuacao e desempate.
- Cadastro e login por email/senha.
- Palpites por rodada com autosave, salvamento manual em lote e bloqueio por horario de partida.
- Palpites especiais com rascunho, finalizacao unica e comprovante baixavel.
- Palpites especiais liberados ate o fim da 3a rodada da fase de grupos.
- Finalizacao de rodada com confirmacao explicita e fechamento automatico por inicio das partidas.
- Comprovante de auditoria em PNG bloqueado enquanto a rodada nao estiver finalizada.
- Area administrativa protegida por senha para registrar resultado oficial uma unica vez por partida.
- Tabela inicial da Copa 2026 com fase de grupos e cruzamentos do mata-mata por posicao.
- Mural publico de mensagens com publicacao, exclusao pelo proprio autor ou Admin e paginacao AJAX com 5 mensagens por pagina.
- Interface responsiva para dispositivos moveis.
- CSS separado por arquivos de base, layout, componentes, paginas e responsividade.
- Deploy preparado para Render com Docker customizado, sem dependencia de `mcr.microsoft.com`.

## Como rodar

Local com Supabase/PostgreSQL:

```bash
dotnet run --project BolaoCopa2026.csproj --urls http://localhost:5086
```

O arquivo `appsettings.Local.json` na raiz do projeto e carregado automaticamente quando existe. Em desenvolvimento ele e a fonte local de banco, e deve conter a connection string do PostgreSQL/Supabase no formato Npgsql e a senha do admin para teste local.

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

## Regras principais

- Placar exato vale 5 pontos.
- Acerto do resultado vale 3 pontos.
- Em mata-mata, acerto do classificado vale 2 pontos adicionais.
- Jogos do Brasil dobram a pontuacao.
- Rodadas futuras respeitam desbloqueio progressivo.
- Cada partida trava automaticamente no horario oficial de inicio.
- Rascunhos ja salvos viram definitivos quando a partida comeca.

## Auditoria e Mural

- O comprovante de auditoria e gerado no servidor em PNG via `SkiaSharp`.
- O download usa `fetch` + blob para evitar loading infinito em links de arquivo.
- O mural em `/Mural` mostra primeiro apenas os participantes e carrega os palpites completos sob demanda ao expandir cada usuario.

## Supabase/PostgreSQL

O projeto usa exclusivamente PostgreSQL/Supabase em desenvolvimento e producao.

Para Supabase em producao, configure variaveis de ambiente no servidor:

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

O projeto esta preparado para deploy no Render usando `Dockerfile` customizado e `render.yaml`.

Passos:

1. Suba este repositorio para o GitHub.
2. No Render, crie um novo Web Service a partir do repositorio.
3. Escolha Docker/free instance.
4. Configure a variavel secreta `SUPABASE_DATABASE_URL` com a URL real do Supabase.
5. Confirme o deploy.

O `Dockerfile` atual usa `debian:bookworm-slim`, instala o .NET com `dotnet-install.sh` e adiciona `libicu`, evitando os erros que ocorriam ao puxar imagens do `mcr.microsoft.com`.

As demais variaveis ficam em `render.yaml`: `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://0.0.0.0:10000`.

Variaveis recomendadas no Render:

```text
SUPABASE_DATABASE_URL=postgresql://postgres.PROJECT_REF:SENHA@HOST:5432/postgres
Admin__Password=uma-senha-de-admin
```

O Render vai gerar uma URL publica do tipo `https://nome-do-servico.onrender.com`.

## Requisitos

- .NET SDK 9.0 ou superior.
