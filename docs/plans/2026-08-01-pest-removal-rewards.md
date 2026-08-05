# ExecPlan — Recompensa por remoção de lagarta

Status: Implementado e validado

Data: 2026-08-01

## Objetivo

Recompensar uma remoção manual bem-sucedida de lagarta com moedas comuns e XP,
tanto na própria fazenda quanto na fazenda de um amigo, sem transformar a
praga em uma fonte dominante de progressão.

O backend continua sendo a única autoridade sobre autorização, estado da
infestação, limite, moedas e XP. React coordena a ação confirmada e o estado
compartilhado; Phaser apresenta apenas os valores devolvidos pelo servidor.

## Decisão consolidada

Veredito: `Prototype`.

- cada transição manual `Active -> Removed` concede inicialmente 2 moedas e
  5 XP ao ator;
- dono e visitante aceito recebem exatamente a mesma recompensa;
- os primeiros 15 resgates recompensados do ator, em uma janela móvel de
  24 horas, concedem recursos;
- depois do limite, a remoção continua permitida e bem-sucedida, com ganhos
  iguais a zero;
- colheita, roubo, proteção, consumo e demais resoluções não concedem a
  recompensa;
- duas tentativas concorrentes sobre a mesma ocorrência produzem uma única
  conclusão e uma única recompensa;
- um retry com a mesma chave idempotente devolve o resultado persistido.

Esta decisão substitui a regra original do MVP que não concedia recursos pela
remoção manual.

## Persistência

Cada infestação recebe um `PestOccurrenceId` estável durante o ciclo. Uma nova
entidade `PestRemovalCompletion` registra toda remoção manual concluída,
inclusive as que ocorrerem depois do limite e concederem zero:

- ocorrência, chave idempotente e ator;
- proprietário, fazenda e lote;
- instante da remoção;
- moedas e XP concedidos;
- saldo de moedas após a conclusão.

Restrições:

- ocorrência única;
- chave idempotente única por ator;
- índice por ator e instante para a janela móvel;
- ganhos não negativos.

O identificador da ocorrência é apagado somente ao reiniciar o ciclo de praga
no próximo plantio. Estados terminais preservam o identificador até lá.

## Transação e concorrência

Ordem canônica de locks:

```text
ator User
-> Farm
-> Plots em ordem estável
-> Inventory
-> contagem e inserção da completion
-> SaveChanges
-> Commit
```

O lock do ator serializa a quota global entre fazendas. Estado da praga,
moedas, XP, ledger, notificação e resposta idempotente pertencem à mesma
transação. Uma tentativa perdedora não recebe prêmio nem consome quota.

## Contrato HTTP

```http
POST /farms/{farmId}/plots/{plotId}/pest/remove
Idempotency-Key: <uuid>
Content-Type: application/json

{ "pestOccurrenceId": "<uuid>" }
```

A resposta existente recebe campos aditivos:

```text
completionId
pestOccurrenceId
coinsGained
xpGained
coins
rewardGranted
```

Os valores ganhos e `rewardGranted` são persistidos e retornados pelo backend.
O cliente nunca infere recompensa a partir do estado da praga ou do limite.
O `pestOccurrenceId` do comando é validado sob lock e impede que uma
requisição atrasada remova uma infestação de um ciclo posterior.

## Integração React e Phaser

Após confirmação:

1. React aplica o XP no `UserProvider`;
2. React solicita reconciliação do inventário e da fazenda;
3. React emite `plot:pest:remove:done`, um evento confirmado de remoção com
   `pestOccurrenceId` e os ganhos do servidor;
4. React fecha o modal antes de uma conclusão nova emitir o evento; replay
   apenas reconcilia os estados compartilhados;
5. Phaser mostra feedback leve de moedas e XP somente quando os valores são
   positivos;
6. após o teto, o feedback confirma que a plantação foi salva sem criar números
   locais.

## Etapas

1. Adicionar opções configuráveis, identidade da ocorrência e ledger.
2. Criar migration, constraints e índices.
3. Tornar o endpoint idempotente e a recompensa atômica.
4. Cobrir dono, visitante, limite, retry e concorrência no backend.
5. Atualizar tipos, estado compartilhado e feedback do modal.
6. Adicionar feedback confirmado no Phaser.
7. Atualizar documentação arquitetural e de gameplay.
8. Executar builds, testes e revisões de arquitetura, gameplay e QA.

## Validação obrigatória

- remoção do dono concede 2 moedas e 5 XP;
- remoção do visitante concede os mesmos valores;
- remoção não manual não concede recursos;
- a 15ª remoção na janela paga e a 16ª não paga;
- a remoção após o limite continua resolvendo a praga;
- passado o início da janela móvel, a quota volta a ficar disponível;
- retry com a mesma chave devolve a conclusão original;
- a mesma chave em outra ocorrência é rejeitada;
- duas requisições sobre a mesma ocorrência concedem uma única recompensa;
- remoções concorrentes em fazendas diferentes respeitam o teto global;
- falha de inventário reverte também a remoção;
- o frontend exibe apenas os ganhos confirmados pelo backend.

## Rollout

1. aplicar a migration;
2. publicar o backend antes do frontend;
3. validar remoção própria e visitada;
4. monitorar emissão diária, concentração entre pares e frequência do teto;
5. ajustar exclusivamente as opções centralizadas após os testes de
   balanceamento.

Rollback operacional mantém a tabela de auditoria e permite zerar os valores
de recompensa ou desabilitar a feature sem remover dados históricos.

## Validação concluída

- migration `20260802000427_AddPestRemovalRewards` aplicada em PostgreSQL
  temporário isolado;
- suíte backend completa: 96 aprovados, zero falhas e zero ignorados;
- testes de pragas: 74 aprovados;
- testes focados da recompensa e da precondição de ocorrência: 9 aprovados;
- `dotnet build Colheita.sln --no-restore`: sucesso, sem warnings;
- `dotnet ef migrations has-pending-model-changes`: nenhum drift;
- testes frontend: 28 aprovados;
- lint e build frontend: sucesso, mantendo apenas o warning conhecido de
  tamanho do chunk Phaser;
- `git diff --check` e `git diff --cached --check`: sucesso;
- revisão de arquitetura: nenhum finding remanescente;
- revisão de gameplay: experiência aprovada;
- QA final: nenhum finding funcional, com a ressalva não bloqueante de ainda
  não existir um teste automatizado direto do bridge `PlotModal -> FarmScene`.
