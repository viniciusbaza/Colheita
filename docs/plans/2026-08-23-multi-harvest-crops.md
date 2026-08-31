# ExecPlan — Culturas de múltiplas colheitas

Status: Implementado e validado

Data: 2026-08-23

## Objetivo e veredito

Veredito: `Prototype` aprovado com salvaguardas.

A V1 permite que uma Seed produza um número finito de vezes sem novo plantio.
Ela é genérica para culturas e apresenta “Colheita contínua” somente na UI quando
`HarvestCycles > 1`. O backend permanece autoridade de tempo, ciclo, yield,
inventário e XP.

## Escopo entregue

- `Seed.CropName`, `HarvestCycles` e `RegrowTime`;
- `Plot.CurrentHarvestCycle` e `CurrentHarvestCycleStartedAt`;
- transição centralizada em `CropCycleRules`;
- `RemainingYield`, praga e cuidado reiniciados por ciclo produtivo;
- precondição `expectedHarvestCycle` no harvest recorrente;
- contratos explícitos de catálogo e estado pós-harvest;
- loja, modal, patch React e três estados visuais no Phaser;
- migration incremental após `20260818050938_InitialCreate`;
- Macieira de nível 5 com três produções;
- Tomate convertido para duas produções, com dois minutos entre ciclos.

Ficam fora da V1: produção/tempo variável por ciclo, ciclos infinitos,
degradação, poda, estações, remoção antecipada complexa, monetização e novos
sistemas de praga.

## Configuração inicial

| Campo | Macieira | Tomate |
| --- | --- | --- |
| Seed/Crop | `apple_tree` / `apple_crop` | `tomato` / `tomato_crop` |
| Nome/CropName | Macieira / Maçã | Tomate / Tomate |
| Crescimento inicial | 2 horas | 2 minutos |
| Novo ciclo | 1 hora | 2 minutos |
| Colheitas | 3 | 2 |
| Produção por ciclo | 3 | 4 |
| Nível/compra/venda | 5 / 90 / 30 | 3 / 30 / 60 |
| XP | 10 no plantio; 125 por harvest | 10 no plantio; 75 por harvest |

Os números são provisórios. Uma vida completa produz no máximo nove maçãs,
270 moedas brutas e 385 XP. Uma vida completa do Tomate produz no máximo oito
tomates, 480 moedas brutas e 160 XP.

## Invariantes

- `GrowTime > 0`, `CropAmount >= 1` e `HarvestCycles >= 1`;
- ciclo único exige `RegrowTime = null`;
- múltiplos ciclos exigem `RegrowTime > 0`;
- Plot vazio não possui ciclo nem início de ciclo;
- Plot ocupado possui ciclo entre 1 e o total configurado;
- o ciclo avança somente após harvest validado;
- `PlantedAt` nunca muda durante regrow;
- `ReadyAt` e `RemainingYield` sempre descrevem o ciclo corrente;
- estado inválido falha antes de qualquer recompensa.

## Fluxo e responsabilidades

Plantio consome uma Seed, inicia ciclo 1, usa `GrowTime`, cria yield, praga e
cuidado do ciclo e concede o XP de plantio.

Harvest intermediário, sob a transação econômica existente, processa pragas
vencidas, valida ciclo/deadline/yield, concede crop e XP e inicia o ciclo
seguinte com novo `ReadyAt`, yield completo, praga resetada e novo
`CareOpportunityId`. Harvest final concede rewards e esvazia a plantação.

React aplica a resposta confirmada, emite `farm:sync`, dispara feedback visual
e reconcilia via GET. Phaser deriva `empty`, solo recém-plantado, `sprout`,
`mature` e `ready` sem persistir máquina de estados. No ciclo inicial, solo
plantado ocupa 0–25%, muda 25–50% e planta adulta sem produção 50–100%; ciclos
posteriores usam `mature` até a confirmação de prontidão. Um timer exclusivo do
Phaser atualiza os dois limites intermediários sem promover `isReady`; o
scheduler autoritativo continua acompanhando cada novo `ReadyAt`. O chão do
Plot e a cultura são objetos separados: o chão mantém sua área fixa, enquanto
`cropVisuals.ts` controla textura, altura e offset de cada cultura. A área
transparente de input continua presa ao lote, e não à copa.

## Persistência e rollout

A migration aditiva cria as cinco colunas, defaults e constraints, faz
backfill de seeds e plots existentes, converte o Tomate para dois ciclos e
insere a Macieira. Nenhum índice é necessário. O rollout é coordenado:
migration, backend e frontend compatível antes de disponibilizar as culturas
recorrentes. Depois de existir um Plot em ciclo maior que 1, rollback exige
migração de dados.

## Integridade econômica

É preservada a ordem de locks:

```text
User -> Farm -> Plots ordenados -> Inventory/Items -> SaveChanges -> Commit
```

Avanço, deadline, yield, cuidado, praga, inventário e XP fazem parte da mesma
transação. Dois harvests do mesmo ciclo não podem conceder reward duas vezes;
harvest, steal e pest concorrentes observam a mesma linha bloqueada. Não foi
adicionado ledger de idempotência de harvest. Harvest e roubo calculam a soma
do inventário em `long` antes de persistir; overflow retorna conflito e faz
rollback integral. O banco também rejeita quantidade negativa e combinações
ocupado/vazio com timestamps ou yield contraditórios.

## Validação e critérios de aceite

- Cenoura, Milho e Abóbora continuam esvaziando no primeiro harvest;
- o Tomate entrega exatamente dois ciclos de quatro unidades, com dois minutos
  de crescimento inicial e dois minutos de regrow, e esvazia no segundo;
- a Macieira entrega exatamente três ciclos e esvazia no terceiro;
- cada ciclo reinicia yield, praga e cuidado sem limpar proteção;
- roubo/praga do ciclo anterior não reduz o ciclo seguinte;
- XP de plantio ocorre uma vez e XP de harvest em cada ciclo;
- requests concorrentes ou com ciclo antigo não duplicam economia;
- loja comum não mostra metadados perenes;
- React/Phaser mostram `Ready -> Regrowing -> Ready` sem refresh manual;
- migration funciona em schema vazio e em upgrade com plots ocupados;
- build, testes, lint e validação PostgreSQL passam.

## Métricas e riscos

Monitorar conclusão por ciclo, yield e XP por Plot-hora, participação frente a
culturas tradicionais, perdas acumuladas, visitas/interações por plantação e
compreensão do regrow. O principal risco é a multiplicação econômica aliada à
menor recompra de sementes.
