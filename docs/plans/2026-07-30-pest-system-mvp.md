# ExecPlan — Sistema de pragas (lagarta)

Status: Implementado e validado

Data: 2026-07-30

Emenda (2026-08-01): a regra original sem recompensa para remoção manual foi
substituída por `docs/plans/2026-08-01-pest-removal-rewards.md`. A remoção
manual agora usa a recompensa limitada de XP e moedas comuns, sempre calculada
pelo servidor, definida naquele plano.

Responsáveis:

- Game Director;
- Social Designer;
- Economy Designer;
- Technical Planner;
- Backend Engineer;
- Frontend Engineer;
- Phaser Engineer;
- Integration Engineer;
- QA Reviewer;
- Architecture Reviewer;
- Gameplay Reviewer.

## Objetivo

Entregar o primeiro corte jogável do sistema de pragas:

```text
Cultura pronta e sem proteção
→ período de segurança
→ lagarta agendada
→ lagarta ativa
→ reação do jogador ou visitante
→ remoção, cancelamento ou consumo de uma unidade
```

O backend permanece responsável por horários, autorização, inventário,
proteção, transições e `RemainingYield`. React apresenta ações e estado;
Phaser renderiza o mundo a partir de `farm:sync`.

## Decisão consolidada

Veredito: `Prototype`.

O conceito reforça retorno à fazenda, descoberta em visitas e uma ação social
gratuita. O protótipo aceita riscos de balanceamento para validar o loop, mas
mantém todos os valores centralizados e uma feature flag para permitir ajuste
ou desligamento.

Quando trechos da proposta se contradizem, os critérios numerados e os testes
solicitados têm precedência:

- um roubo anterior não impede uma futura infestação;
- roubo durante `Scheduled` não cancela o agendamento;
- roubo durante `Active` cancela a infestação;
- se um roubo deixar somente uma unidade antes da ativação, a infestação
  termina como `CancelledByTheft` sem dano;
- roubo e praga podem afetar o mesmo ciclo em momentos diferentes, mas jamais
  reduzem a produção abaixo de uma unidade.

## Configuração inicial

| Configuração | Valor inicial |
|---|---:|
| Período de segurança | 15 minutos |
| Janela de reação | 15 minutos |
| Dano | 1 unidade |
| Máximo de pragas ativas por fazenda | 9 |
| Intervalo mínimo entre aparecimentos | 1 minuto |
| Item | `natural_repellent` |
| Nome | Repelente Natural |
| Preço | 30 moedas comuns |
| Proteção | 4 horas |
| Empilhamento de proteção | não permitido |

Não há sorteio de probabilidade no MVP. Todo lote elegível entra
deterministicamente na fila, respeitando o limite e o intervalo configurados.

O kill switch é fail-closed: ~~`Pests.Enabled` é `false` no
`appsettings.json` base e `true` somente no
`appsettings.Development.json`~~. Com a flag desligada não há agendamento,
ativação, consumo, catálogo, compra nem aplicação de proteção. A remoção
gratuita de uma lagarta já ativa permanece disponível para não prender o
jogador a um estado legado.

## Estado e persistência

O estado de uma única infestação por ciclo fica diretamente em `Plot`.
Uma entidade separada adicionaria histórico e relacionamentos que o MVP não
consulta.

Campos persistidos no lote:

- tipo e status da praga;
- instante do agendamento;
- instante previsto e real do aparecimento;
- deadline de consumo;
- instante e motivo da resolução;
- quantidade efetivamente consumida;
- `ProtectedUntil`.

`HasHadPestThisCycle` não é armazenado: qualquer status diferente de `None`
já prova que o ciclo teve uma infestação.

`Farm.LastPestInfestationAt` preserva o espaçamento entre aparecimentos mesmo
quando um lote é colhido e replantado.

Estados:

```text
None
Scheduled
Active
Removed
Consumed
CancelledByTheft
CancelledByHarvest
CancelledByProtection
```

Estados terminais permanecem até o próximo plantio. O plantio reseta somente
o ciclo de praga; `ProtectedUntil` permanece enquanto não expirar.

## Fonte de verdade do yield

`Plot.RemainingYield` passa a ser a única fonte persistida da produção atual:

- plantio inicializa com `Seed.CropAmount`;
- roubo reduz o valor persistido;
- praga reduz o valor persistido;
- colheita entrega exatamente o valor persistido;
- colheita limpa o valor ao esvaziar o lote.

`TheftLog` continua sendo histórico e base para limites de roubo, não para
reconstruir a produção. O carregamento da fazenda não recalcula o yield por
logs.

## Processamento temporal

Não haverá background worker.

O servidor processa pragas em carregamento/sincronização e antes de ações que
tocam o lote. Um único `now` UTC é capturado por operação.

Semântica lazy:

1. durante o período de segurança, o lote permanece `None`;
2. após a segurança, o lote é agendado;
3. quando limite e intervalo permitem, ele se torna `Active`;
4. a janela integral de reação começa no aparecimento real;
5. uma praga `Active` já persistida cujo deadline venceu consome uma única vez.

Isso evita perda retroativa antes de existir uma janela observável, sem mover
autoridade para o cliente.

## Concorrência e idempotência

Ordem canônica de locks:

```text
ator User, quando a economia muda
→ Farm
→ Plots em ordem estável
→ Inventory/InventoryItem
→ SaveChanges
→ Commit
```

As linhas são serializadas com `SELECT ... FOR UPDATE` no PostgreSQL.

Resultados esperados:

- dois processamentos vencidos consomem uma única vez;
- harvest antes do deadline cancela e colhe;
- harvest após o deadline aplica o consumo e colhe o restante;
- roubo contra `Active` reduz o yield pelo roubo e cancela a lagarta;
- consumo que vencer primeiro reduz o yield antes de um roubo posterior;
- proteção vencida não retroage;
- duas aplicações concorrentes consomem no máximo um item;
- retry de remoção não concede recurso;
- proteção já válida retorna conflito sem consumir item.

## Contratos HTTP

### Fazenda

`GET /farms/my` e `GET /farms/{farmId}` recebem o deadline aditivo
`nextPestCheckAt` no nível da fazenda e os seguintes campos no lote:

```text
protectedUntil
pest {
  type
  status
  scheduledAt
  appearsAt
  appearedAt
  consumesAt
  resolvedAt
  consumedAmount
  canRemove
}
```

`pest` é nulo quando o estado é `None`.

### Ações

```http
POST /farms/{farmId}/plots/{plotId}/pest/remove
POST /farms/{farmId}/plots/{plotId}/pest/protection
```

Ambos usam o usuário autenticado. A proteção sempre consome o item do ator,
inclusive em uma fazenda visitada.

Erros de praga usam `ProblemDetails` com códigos estáveis:

```text
PEST_FEATURE_DISABLED
PEST_FARM_NOT_FOUND
PEST_PLOT_NOT_FOUND
PEST_FORBIDDEN
PEST_NOT_ACTIVE
PEST_ALREADY_PROTECTED
PEST_INVENTORY_NOT_FOUND
PEST_ITEM_UNAVAILABLE
```

O endpoint de roubo agora devolve `pestCancelled`. O cliente só apresenta o
feedback de lagarta espantada quando esse valor confirmado pelo backend for
`true`.

### Loja

```http
GET  /shop/items
POST /shop/buy-item
```

O catálogo do MVP é derivado da configuração do backend. A quantidade
comprada é armazenada como `InventoryItem` com `ItemType.Item`.

## Integração React e Phaser

React:

- atualiza os tipos do contrato;
- agenda refresh no menor deadline de crescimento, cuidado, praga ou proteção;
- apresenta countdowns sem confirmar transições localmente;
- executa remoção e proteção;
- atualiza farm e inventário após confirmação;
- apresenta o repelente na loja e no inventário.

Phaser:

- recebe a fazenda completa por `farm:sync`;
- mostra lagarta apenas em `Active`;
- mostra proteção válida de modo discreto;
- atualiza o badge de yield por sincronização;
- anima consumo confirmado sem calcular dano;
- remove containers e tweens ao trocar de estado ou encerrar a cena.

Não será criado um segundo estado autoritativo nem um endpoint chamado
diretamente pelo Phaser.

## Etapas

1. Corrigir a autoridade de `RemainingYield`.
2. Adicionar configuração, enums, regras e `PestService`.
3. Adicionar persistência e migration.
4. Integrar plantio, colheita, roubo e carregamento da fazenda.
5. Adicionar endpoints de remoção e proteção.
6. Adicionar catálogo e compra do repelente.
7. Atualizar contratos, deadlines e modal React.
8. Atualizar loja, inventário e notificações.
9. Adicionar visual e sincronização Phaser.
10. Executar integração e corrigir divergências.
11. Revisar arquitetura, gameplay e qualidade.
12. Validar migration, testes, builds e cenários manuais.

## Testes

Cobertura obrigatória:

- elegibilidade por crescimento, segurança, yield e proteção;
- todos os estados e cancelamentos;
- uma infestação por ciclo;
- remoção por dono e visitante;
- consumo único com piso de yield;
- roubo em `Scheduled` e `Active`;
- colheita antes e depois do deadline;
- consumo atômico do repelente;
- proteção sobrevivendo à troca de cultura;
- roubo permitido em lote protegido;
- corridas entre roubo, colheita, consumo e proteção;
- cálculo do próximo deadline no frontend;
- sincronização repetida sem objetos Phaser duplicados.

Comandos de validação:

```powershell
dotnet build Colheita.sln
dotnet test Colheita.sln
Set-Location FarmAndFriends.Frontend
npm test
npm run build
npm run lint
```

Testes PostgreSQL condicionais usam uma base isolada configurada por variável
de ambiente. A migration não deve ser aplicada automaticamente ao banco local
durante a implementação.

Validação final concluída em 2026-07-31:

- migration completa aplicada do zero em PostgreSQL 16 descartável;
- upgrade aplicado sobre snapshot legado com lote vazio inconsistente, cultura
  plantada sem yield e cultura com yield parcial preservado;
- `dotnet build Colheita.sln --no-restore`: sucesso, sem warnings;
- `dotnet test Colheita.sln --no-build`: 82 aprovados, 0 falhas, 0 ignorados;
- `dotnet ef migrations has-pending-model-changes`: nenhum drift;
- `npm test`: 25 aprovados;
- `npm run lint`: sucesso;
- `npm run build`: sucesso, com o warning preexistente de chunk grande;
- inspeção real no Edge headless em 1365 × 768 e 390 × 844: lagarta, atalho,
  modal, countdown, remoção e colheita legíveis, sem erro de navegador e sem
  overflow da viewport;
- `git diff --check` e `git diff --cached --check`: sucesso.

## Rollout e rollback

Rollout:

1. aplicar a migration;
2. validar a aplicação com a feature flag desligada, que é o padrão da
   configuração base;
3. habilitar em desenvolvimento;
4. validar fazenda própria e visitada;
5. habilitar no ambiente alvo;
6. monitorar remoções, consumos, perda média e uso do repelente.

Rollback operacional:

- desligar `PestOptions.Enabled`;
- manter o schema aditivo durante estabilização;
- não executar `Down` em produção sem aceitar a perda dos estados e proteções.

## Riscos conhecidos

- 15 + 15 minutos é uma janela agressiva para jogadores casuais;
- roubo anterior mais praga pode causar perda acumulada relevante;
- várias lagartas podem tornar o retorno visualmente e emocionalmente pesado;
- o repelente pode parecer obrigatório se a incidência ficar alta;
- GET passa a materializar transições temporais no banco;
- locks farm-wide aumentam contenção, embora a fazenda atual seja pequena;
- contratos TypeScript manuais podem divergir do backend.

Os valores configuráveis e a feature flag são a mitigação operacional do
protótipo. Métricas e feedback qualitativo devem determinar o rebalanceamento
antes de um rollout amplo.
