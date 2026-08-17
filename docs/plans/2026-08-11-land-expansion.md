# ExecPlan — Expansão de terrenos 3×3 para 7×4

Status: Implementado e validado

Data: 2026-08-11

## Objetivo

Transformar a progressão econômica em crescimento visível da fazenda. O dono
compra um terreno por vez por meio de uma única placa `VENDE-SE`; o terreno
fica imediatamente disponível para plantio e a placa avança para a próxima
oferta autoritativa.

O backend determina propriedade, sequência, nível, preços, saldo e topologia.
React coordena confirmação, API e reconciliação. Phaser renderiza a placa e a
grade a partir do `FarmResponse` confirmado.

## Decisão consolidada

Veredito: `Prototype`.

- a fazenda começa em 3×3 com seis terrenos desbloqueados;
- a sequência inicial é `(0,2)`, `(1,2)`, `(2,2)`;
- a compra do nono terreno adiciona, na mesma transação, os 19 espaços
  bloqueados que completam 7 colunas × 4 linhas;
- a sequência 7×4 começa em `(0,3)`, `(1,3)`, `(2,3)` e continua pelas colunas
  `x=3..6`, sempre de cima para baixo;
- cada oferta exige nível mínimo e pode ser paga com moedas comuns ou premium;
- premium não ignora nível e não recebe nova fonte nesta entrega;
- visitantes não recebem a oferta, mas veem todos os plots persistidos da
  topologia atual e quais ainda estão bloqueados;
- os 19 novos plots ficam visíveis de uma vez para o dono;
- depois do 28º terreno, a placa desaparece e não anuncia estágio futuro;
- cuidado, roubo, pragas e `RemainingYield` preservam as regras atuais;
- todos os cuidados elegíveis continuam recompensando dentro do ciclo atual,
  sem teto adicional por tamanho da fazenda.

## Curva econômica inicial

| Terrenos | Nível mínimo | Moedas por terreno | Premium por terreno |
| -------- | ------------ | ------------------ | ------------------- |
| 7        | 2            | 500                | 2                   |
| 8        | 3            | 1.500              | 3                   |
| 9        | 4            | 4.000              | 4                   |
| 10–13    | 5            | 10.000             | 5                   |
| 14–18    | 6            | 25.000             | 6                   |
| 19–23    | 7            | 60.000             | 8                   |
| 24–28    | 8            | 150.000            | 10                  |

O alvo inicial é permitir que um jogador casual conclua o 7×4 em 12 a 24
semanas pela rota gratuita. Os 10 premium iniciais permitem completar os três
terrenos restantes do 3×3 por 9 premium, sem tornar esse gasto obrigatório.

## Persistência e integridade

Uma regra pura centraliza as coordenadas canônicas, reconhecimento do estágio,
sequência e faixa econômica. O cadastro usa a mesma regra, evitando uma segunda
definição da grade inicial.

Cada compra bem-sucedida cria uma `LandPurchaseCompletion` contendo:

- comprador, fazenda e plot;
- chave idempotente;
- número do terreno;
- moeda e valor gastos;
- saldos após a compra;
- instante da conclusão;
- quantidade de plots adicionados e dimensões da expansão, quando aplicável.

Restrições obrigatórias:

- `(FarmId, X, Y)` único;
- coordenadas não negativas;
- uma completion por plot;
- chave idempotente única por comprador;
- valores gastos positivos;
- saldos e quantidade adicionada não negativos.

Layouts fora dos conjuntos canônicos ou com desbloqueios fora do prefixo da
sequência não são corrigidos automaticamente. Eles não recebem oferta, geram
log estruturado e rejeitam compra com conflito.

## Transação e idempotência

Ordem canônica:

```text
Buyer User
-> Farm
-> Plots em ordem estável
-> recalcular oferta sob lock
-> Inventory
-> debitar uma moeda
-> desbloquear exatamente um plot
-> adicionar 19 plots se o 3×3 acabou
-> persistir LandPurchaseCompletion
-> Commit
```

A mesma chave com o mesmo plot e moeda reproduz a conclusão persistida. A mesma
chave com outro alvo ou moeda é rejeitada. Chaves concorrentes para a mesma
oferta podem produzir apenas um débito, um desbloqueio e uma expansão.

## Contrato HTTP

`GET /farms/my` recebe o campo aditivo `landOffer`:

```json
{
  "plotId": "uuid",
  "plotNumber": 7,
  "maxPlots": 28,
  "minLevel": 2,
  "prices": {
    "coins": 500,
    "premiumCoins": 2
  },
  "expandsTo": null
}
```

`expandsTo` contém `{ "columns": 7, "rows": 4 }` apenas na oferta do
nono terreno. A oferta é nula depois do 28º, com a feature desligada, para
visitantes e em layouts incompatíveis.

```http
POST /plots/{plotId}/purchase
Idempotency-Key: <uuid>
Content-Type: application/json

{ "paymentCurrency": "coins" }
```

`paymentCurrency` aceita `coins` ou `premiumCoins`. O servidor não recebe
preço, nível, saldo, usuário, fazenda, coordenada ou número do terreno.

Resposta:

```json
{
  "completionId": "uuid",
  "plotId": "uuid",
  "plotNumber": 7,
  "paymentCurrency": "coins",
  "amountSpent": 500,
  "addedPlotCount": 0,
  "expandedTo": null,
  "replayed": false
}
```

Erros usam `ProblemDetails` com códigos estáveis para chave ausente/reutilizada,
feature desligada, oferta desatualizada, nível insuficiente, saldo insuficiente,
layout incompatível e plot fora da fazenda própria.

## Integração React e Phaser

1. `plot:click` seleciona o plot que contém a placa.
2. React exibe nível, progresso, os dois preços e os saldos atuais.
3. O jogador escolhe a moeda e aciona `Comprar` na mesma tela; selecionar a
   moeda isoladamente nunca inicia a compra. O aviso da expansão `7 × 4` aparece
   apenas na oferta que completa a grade inicial `3 × 3`.
4. React preserva a chave idempotente durante retry do mesmo plot e moeda.
5. Depois da confirmação do backend, React entra em reconciliação e só anuncia
   sucesso quando um snapshot autoritativo mostra o lote desbloqueado. Falha do
   refresh preserva a chave para replay; inventário atualiza em seguida, de
   forma best-effort.
6. `farm:sync` atualiza a textura, move a placa e reconstrói a cena se a
   topologia passar para 7×4.
7. Phaser mantém um único sprite estático da placa e nunca calcula oferta ou
   preço.

Não é necessário evento React–Phaser novo. Permanecem `plot:click`, `ui:modal`,
`farm:sync` e `inventory:changed`.

## Etapas

1. Consolidar regras, opções, contratos e criação inicial no backend.
2. Criar ledger, constraints e migration.
3. Implementar serviço e endpoint transacionais.
4. Cobrir ordem, economia, autorização, replay e concorrência.
5. Atualizar tipos e fluxo idempotente no React.
6. Adicionar ação de compra acessível em uma única tela e feedback in-game.
7. Gerar e integrar a placa raster no Phaser.
8. Atualizar documentação de produto, gameplay e arquitetura.
9. Executar builds, testes PostgreSQL, validação visual e revisões finais.

## Validação obrigatória

- os seis terrenos iniciais e seus IDs permanecem intactos;
- cada oferta aponta para a coordenada canônica seguinte;
- nível é obrigatório nas duas moedas;
- cada meio de pagamento debita apenas seu próprio saldo;
- saldo insuficiente não modifica nenhuma tabela;
- retry devolve a conclusão original;
- concorrência não cobra nem adiciona plots duas vezes;
- a compra do nono cria exatamente 19 plots bloqueados e vazios;
- os 28 pares de coordenadas são únicos;
- a oferta desaparece depois do 28º;
- visitante recebe os 9 ou 28 plots da topologia atual, incluindo bloqueados,
  e nenhuma oferta;
- `farm:sync` move ou remove a placa sem F5 e sem duplicação;
- 7×4 permanece enquadrável em desktop e celular;
- todas as recompensas de cuidado elegíveis continuam funcionando conforme a
  decisão econômica aprovada.

## Rollout e métricas

`LandExpansion.Enabled` funciona como kill switch e começa ativo por padrão.
Desligá-lo bloqueia somente novas ofertas e compras; terrenos existentes,
completions e saldos permanecem preservados.

O banco descartável de desenvolvimento foi consolidado em um baseline que já
representa a topologia e a sequência aprovadas. Ele suporta somente os layouts
canônicos 3×3 e 7×4 descritos neste plano; protótipos descartados não possuem
caminho de upgrade.

O reset local recria o volume PostgreSQL e aplica as migrations versionadas.
Ele não apaga nem regenera o histórico. Qualquer mudança persistente futura
deve adicionar uma migration incremental a partir deste baseline.

Monitorar:

- tempo até os terrenos 7, 9, 13 e 28;
- moeda escolhida e falhas de nível/saldo;
- utilização dos terrenos novos;
- emissão de moedas e XP por tamanho de fazenda;
- recompensas de cuidado e perdas por roubo em fazendas expandidas;
- replays, conflitos e layouts incompatíveis.

Duplicidade, saldo negativo ou coordenada repetida exige desligamento imediato.
Conclusão mediana abaixo de 6 ou acima de 24 semanas exige rebalanceamento.

## Resultado da implementação

A fatia vertical foi concluída em backend, PostgreSQL, React e Phaser, incluindo
a arte raster da placa, kill switch, contratos aditivos, migration, ledger
idempotente, compra em uma única tela e reconciliação sem F5.

Evidências finais:

- build .NET sem erros ou avisos e modelo sem mudanças pendentes;
- teste PostgreSQL do baseline e 10 integrações PostgreSQL da expansão
  aprovados em bancos isolados;
- 90 testes backend não PostgreSQL aprovados;
- 44 testes frontend, lint e build de produção aprovados;
- baseline atual aplicado com sucesso em um PostgreSQL vazio;
- fluxo visual exercitado em desktop e 390×844, incluindo a placa estática,
  transição 3×3→7×4 com 19 bloqueados e remoção após o lote 28;
- revisões finais de arquitetura, gameplay e QA sem achado bloqueante.

A suíte PostgreSQL completa ainda contém oito asserts antigos de cuidado que
esperam 2 XP, enquanto configuração e runtime concedem 5 XP. Essa divergência
é anterior e independente da expansão de terrenos.
