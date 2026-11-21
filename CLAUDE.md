# PKVault (fork) — guia para Claude e agentes

Fork do [Chnapy/PKVault](https://github.com/Chnapy/PKVault), um gerenciador de pokémons e saves baseado no PKHeX.
Objetivo do fork: um "PokéHome" open source e grátis para um grupo pequeno (~3 pessoas). O app roda nativo e local. Um servidor PocketBase guarda os backups e cuida das features sociais.

Leia este arquivo inteiro antes de mudar qualquer coisa.

## Regras do fork

1. **Não trocar a stack.** Frontend: React 19 + Mantine 9 + CSS Modules. Backend: .NET 10.
   - Proibido adicionar Tailwind, Radix, shadcn/ui ou outra biblioteca de componentes.
   - O visual "estilo shadcn" é só **linguagem visual**: paleta neutra, bordas de 1px, raio ~8px, espaçamento generoso. Tudo isso é feito com tema e CSS do Mantine.
2. **O frontend é fork definitivo.** Não tentamos manter o frontend mergeável com o upstream.
3. **O backend deve continuar mergeável.** Trazemos correções do Core e atualizações do PKHeX do upstream. Por isso:
   - Código novo vai em **pastas novas** dentro de `PKVault.Core/` (ex.: `save-discovery/`, `cloud/`).
   - Arquivos que já existem no upstream recebem só o mínimo necessário: registrar serviços em `Program.cs`, registrar controllers em `router/CoreRouter.cs`, chamar um hook.
4. **Nunca alterar a estrutura de navegação por gamepad e teclado.** Em restilização, mexa só no CSS ao redor. Isso inclui:
   - `useFocusControls`, `FocusScope`, `WithControlsIcons`
   - `src/ui/interaction/`
   - os atributos `data-focused` e `data-show-focus`
   - as variáveis `--focus-color-*`
5. **Todo texto visível passa pelo i18n** (`frontend/src/translate/locales/*.json`). Os idiomas-fonte são `en` e `pt-br`.

## Arquitetura

| Projeto | Papel |
|---|---|
| `PKVault.Core` | Toda a lógica: PKHeX, EF Core + SQLite, sessão e ações, backups, Pokédex. Docs em `PKVault.Core/docs/` (ARCHITECTURE, SESSION, DATA) |
| `PKVault.Backend` | Host ASP.NET para web e Docker. Em Debug, reescreve `PKVault.Core/swagger.json` a cada start |
| `PKVault.Desktop` | Host Photino com o frontend embutido (`Resources/wwwroot`) |
| `PKVault.Mobile` | Host MAUI Android |
| `PKVault.Core.Tests` | Testes do Core |
| `frontend` | React. SDK gerado pelo orval a partir do `swagger.json` |
| `Scripts` | Migrations EF, geração de static-data e atualização do PKHeX |

### Pontos principais do backend
- **Rotas:** classes `*Controller` com atributos próprios (`[Route]`, `[HttpGet]`...). Não é ASP.NET MVC.
  - Um controller novo precisa ser registrado no `CoreRouter.cs` (lista de `typeof`) **e** no `Program.cs` (`services.AddScoped<...>()`).
- **Sessão:** cada ação do usuário é uma `DataAction` (`storage/data-action/`). Ela entra numa fila, pode ser desfeita e só é gravada em disco no "Salvar", que antes cria um zip de backup (`backup/services/BackupService.cs`).
- **Respostas:** as respostas de ação trazem um `DataDTO` com **só o que mudou**. O frontend junta esse delta ao cache.
- **Identidade do pokémon:** `ImmutablePKM.GetPKMIdBase()` gera uma impressão digital (espécie base + PID + IVs + outros dados; DVs na Gen 1/2). Saves com o mesmo IdBase em dois lugares recebem `IsDuplicate`.
- **Migrations:** o Desktop usa trimming, então reflection não funciona. Gerar migrations **só** com `dotnet run --project Scripts gen-migration Nome`.

### Pontos principais do frontend
- `src/ui/` tem os componentes **visuais**: 160 `.tsx`, 33 CSS Modules. Os maiores são `ui/storage`, `ui/form`, `ui/interaction` e `ui/pokedex`.
- `src/storage/`, `src/pokedex/`, `src/settings/` e `src/saves/` têm a lógica e a ligação com os dados.
- **Tema:**
  - `src/ui/base-theme.ts`: cores, fonte e o `cssVariablesResolver`, que gera as variáveis `--pk-*`
  - `src/ui/theme.ts`: padrões dos componentes Mantine
  - `src/ui/theme.module.css` e `src/ui/global.css`
- **Rotas:** TanStack Router, com rotas por arquivo em `src/routes/`. Dados via TanStack Query com hooks gerados em `src/data/sdk/` (gitignored, nunca editar à mão).

## Comandos

```sh
# Backend
dotnet build PKVault.Backend/PKVault.Backend.csproj
dotnet test --project PKVault.Core.Tests/PKVault.Core.Tests.csproj
dotnet run --project PKVault.Backend          # http://localhost:5000 (também regenera swagger.json)

# Frontend (dentro de frontend/)
npm install
npm run gen:sdk                                # depois de mudar DTO ou rota do backend
npm run gen:routes                             # depois de criar ou renomear arquivo em src/routes
npm run gen:css:dts                            # depois de criar ou renomear classe em *.module.css
DOCS_PATH=../docs/functional npm run gen:docs  # obrigatório uma vez; sem isso o c:type falha (docs.gen)
npm run c:translate:fix                        # depois de adicionar ou remover chave de i18n
npm run dev                                    # http://localhost:5173
```

**Abra o app em `http://localhost:5173/` sem `?server=`.** O Vite faz proxy de `/api` para a porta 5000. O backend não tem CORS, então o parâmetro `?server=` dá "Failed to fetch".

Os dados de dev (settings, db, backups) ficam em `PKVault.Backend/bin/Debug/net10.0/`. O save de exemplo é um Emerald.

O `make` não está instalado nesta máquina. Rode os comandos do `Makefile` manualmente.

## Definition of done (toda tarefa)

Rode tudo isto antes de considerar a tarefa pronta:

```sh
dotnet build PKVault.Backend/PKVault.Backend.csproj        # 0 erros, 0 avisos
dotnet test --project PKVault.Core.Tests/PKVault.Core.Tests.csproj
cd frontend && npm run c:type && npm run c:lint && npx vitest run && npm run c:translate
```

Baseline em 2026-11-20 (upstream `17fe85b1`, v2.5.3): 164 testes no backend e 60 no frontend, tudo verde. Uma tarefa não pode deixar isso pior.

O `npm run c:lint:css` está **zerado** desde 2026-11-21 (os 8 erros herdados do upstream em `ui-button.module.css` foram corrigidos). Não podem aparecer erros novos.

Mudanças visuais também passam pelo roteiro em [docs/fork/visual-qa.md](docs/fork/visual-qa.md).

## Trabalho com agentes em paralelo

- Cada agente trabalha num git worktree próprio e numa branch `agent/<tarefa>`, e só mexe nas pastas da sua tarefa.
- **Arquivos compartilhados** são editados só pelo coordenador, depois de juntar o trabalho:
  - `src/ui/theme.ts`, `src/ui/base-theme.ts`, `src/ui/global.css`, `src/ui/form/`
  - `frontend/src/translate/locales/*.json`. O agente lista as chaves novas no relatório final em vez de editar.
  - o SDK gerado e o `swagger.json`
  - `PKVault.Core/Program.cs` e `router/CoreRouter.cs`
- Commits pequenos, com mensagem no padrão `tipo(escopo): descrição` (ex.: `style(saves): ...`, `feat(discovery): ...`).

## Decisões do projeto

- Fonte: **Arial** no lugar da pixelada "Pixel Operator" (`base-theme.ts`, `fontFamily`).
- Identidade visual **neutra, simples e minimalista**:
  - Header, footer e moldura em cinza-zinco, sem fundo colorido.
  - A cor `primary` é **neutra** (estilo shadcn): quase branca no tema escuro, quase preta no claro, com o texto invertido. É um `virtualColor` em `base-theme.ts` (`primaryLight` e `primaryDark`). Vale para ação principal, aba ativa, seleção, filtro ativo e foco.
  - **Vermelho** (`red`, `#dc2626`) só em ações que pedem cuidado: soltar, deletar, remover, restaurar.
  - **Azul** (`blue`, `#2563eb`) só em edição. Busca, envio e "Salvar" não são azuis.
  - Logo: **Ultra Ball** (`frontend/public/logo.svg`, `icon.png`, `icon.ico`). O amarelo dela não é cor de interface.
  - As cores de tipo, jogo e status (`customTheme`) não mudam.
- Ideia futura, ainda **não implementar**: abrir os detalhes do pokémon (status etc.) num **dialog modal estilo shadcn** em vez do painel lateral atual.
- Servidor: PocketBase numa VM Oracle Always Free (AMD micro), atrás de um Caddy com HTTPS e domínio DuckDNS. Login só por OAuth (Google e GitHub).
- Features planejadas, em ordem:
  1. Detecção automática de saves
  2. Backups melhores (`IBackupProvider`)
  3. Backup no servidor + tela de conflitos
  4. Registro de pokémons, histórico e trocas com custódia no servidor
  5. Visualização de leitura pelo celular
  6. Webhook do Discord
