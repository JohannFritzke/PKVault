# Roteiro de checagem visual

O Storybook tem só 17 stories, então as mudanças visuais são validadas no **app rodando**.
Cada agente de redesign segue este roteiro nas telas que tocou e anexa os prints no relatório.

## Preparação

1. `dotnet run --project PKVault.Backend` (porta 5000)
2. `cd frontend && npm run dev`, depois abrir `http://localhost:5173/` (sem `?server=`)
3. Janela de 1568×750 (o desktop típico) e outra estreita, de ~1024 de largura
4. Rodar tudo **duas vezes**: tema escuro e tema claro (botão ☀ no canto superior direito)

## Checagens globais (toda tela)

- [ ] Nenhum erro novo no console do navegador (DevTools → Console).
- [ ] Fonte Arial aplicada. Não sobra texto na fonte pixelada.
- [ ] Contraste legível em claro e escuro: texto atenuado ainda legível, bordas visíveis.
- [ ] As cores de tipo, jogo e status (`customTheme` em `base-theme.ts`) continuam as mesmas. Elas são **identidade**, não tema.
- [ ] Sprites nítidos, sem borrão (`image-rendering` de pixel art preservado).
- [ ] Nenhum texto cortado nem quebrado de forma estranha em pt-br, que tem os textos mais longos.
- [ ] **Navegação por teclado:** as setas movem o foco, o anel de foco é visível e o Enter abre ou ativa.
- [ ] Nada quebra com o "Navegação por gamepad/teclado" desligado nas Configurações.

## Por tela

### Header e footer (todas)
- [ ] As abas Saves / Armazenamento / Pokédex mostram com clareza qual está ativa.
- [ ] Os botões Recarregar, Ajuda, Backups e configurações, notificações e tema funcionam.
- [ ] A barra de banco (Bank 1, estrela e seletor) aparece em Armazenamento.
- [ ] O footer "Ações para salvar" e o botão Salvar ficam visíveis, e o contador de ações aparece depois de mover um pokémon.

### Saves
- [ ] O card do save mostra geração, jogo, OT/TID, contadores, tempo de jogo, idioma e caminho.
- [ ] Os botões de deletar e baixar do card funcionam.
- [ ] Os botões "Buscar arquivos..." e "Enviar arquivos..." abrem o que deveriam.

### Armazenamento (a tela mais crítica)
- [ ] Os dois painéis abrem: PKVault à esquerda e um save à direita.
- [ ] As abas de boxes (Party, BOX1...) rolam e o seletor funciona.
- [ ] **Clique em pokémon abre os detalhes. Testar 5 cliques seguidos em pokémons diferentes, porque já existiu um bug de clique intermitente.**
- [ ] Os detalhes mostram as abas Resumo, Atributos, Movimentos e Torneios, o item segurado e os botões Mover, Mover anexado, Editar e Soltar.
- [ ] **Arrastar** um pokémon do save para a box do PKVault funciona, e a ação aparece no footer.
- [ ] Desfazer a ação funciona.
- [ ] Seleção múltipla e a barra de ações em grupo funcionam.
- [ ] Slots vazios, slot selecionado e "Box Vazia" ficam visualmente distintos.

### Pokédex
- [ ] A grade carrega as 151 ou mais espécies, e o scroll fica fluido.
- [ ] Os filtros da lateral (espécie, status, tipos, armazenamentos, gerações) filtram de verdade.
- [ ] Os contadores do topo (visto, capturado, possuído, shiny) estão corretos.
- [ ] O clique numa espécie abre os detalhes, incluindo a evolução.

### Configurações (Principal, Externos, Backups, Sobre)
- [ ] O idioma troca e o app re-renderiza.
- [ ] Os switches (cheats, legalidade, gamepad) funcionam.
- [ ] A lista de locais de save permite adicionar e remover, e os botões Cancelar e Enviar funcionam.
- [ ] A aba Backups lista e restaura.

## Relatório do agente

- Prints do antes e depois de cada tela tocada, em claro e escuro.
- Lista de itens do roteiro que falharam ou não puderam ser testados, com o motivo.
- Chaves de i18n novas, que o coordenador vai adicionar.
