import assert from 'node:assert/strict'
import test from 'node:test'
import {
  confirmedPestRemovalPatch,
  confirmedTheftPatch,
  getPestRemovalAttempt,
  isCurrentFarmRequest,
  patchFarmPlot,
} from '../src/farm/farmState.ts'
import type {
  Farm,
  PestActionResponse,
  Plot,
} from '../src/types/Farm.ts'

const activePlot: Plot = {
  id: 'plot-1',
  x: 0,
  y: 0,
  unlocked: true,
  seedId: 'corn',
  plantedAt: '2026-07-30T09:00:00.000Z',
  isReady: true,
  readyAt: '2026-07-30T09:10:00.000Z',
  remainingYield: 3,
  state: 'ready',
  care: null,
  protectedUntil: null,
  pest: {
    type: 'caterpillar',
    status: 'active',
    scheduledAt: '2026-07-30T09:20:00.000Z',
    appearsAt: '2026-07-30T09:25:00.000Z',
    appearedAt: '2026-07-30T09:25:00.000Z',
    consumesAt: '2026-07-30T09:40:00.000Z',
    resolvedAt: null,
    consumedAmount: 0,
    canRemove: true,
    occurrenceId: 'occurrence-1',
  },
}

const farm: Farm = {
  id: 'farm-1',
  name: 'Fazenda',
  ownerUserId: 'owner-1',
  ownerUsername: 'owner',
  nextPestCheckAt: null,
  plots: [activePlot],
}

test('patches a plot from confirmed pest action fields without mutating the snapshot', () => {
  const removedPest = {
    ...activePlot.pest!,
    status: 'removed' as const,
    resolvedAt: '2026-07-30T09:30:00.000Z',
    canRemove: false,
  }
  const nextFarm = patchFarmPlot(farm, activePlot.id, {
    pest: removedPest,
    remainingYield: 3,
  })

  assert.notEqual(nextFarm, farm)
  assert.equal(farm.plots[0].pest?.status, 'active')
  assert.equal(nextFarm.plots[0].pest?.status, 'removed')
})

test('confirmed protection patches protection and the returned pest state together', () => {
  const nextFarm = patchFarmPlot(farm, activePlot.id, {
    pest: {
      ...activePlot.pest!,
      status: 'cancelledByProtection',
      resolvedAt: '2026-07-30T09:30:00.000Z',
      canRemove: false,
    },
    protectedUntil: '2026-07-30T15:30:00.000Z',
    remainingYield: 3,
  })

  assert.equal(
    nextFarm.plots[0].protectedUntil,
    '2026-07-30T15:30:00.000Z',
  )
  assert.equal(
    nextFarm.plots[0].pest?.status,
    'cancelledByProtection',
  )
})

function pestRemovalResponse(
  overrides: Partial<PestActionResponse> = {},
): PestActionResponse {
  return {
    plotId: activePlot.id,
    pest: {
      ...activePlot.pest!,
      status: 'removed',
      resolvedAt: '2026-07-30T09:30:00.000Z',
      canRemove: false,
    },
    status: 'removed',
    remainingYield: 3,
    completionId: 'completion-1',
    pestOccurrenceId: 'occurrence-1',
    coinsGained: 2,
    xpGained: 5,
    coins: 102,
    rewardGranted: true,
    replayed: false,
    ...overrides,
  }
}

test('confirmed pest removal patches only the matching occurrence', () => {
  const response = pestRemovalResponse()
  const matching = confirmedPestRemovalPatch(activePlot, response)
  const newerCycle = confirmedPestRemovalPatch(
    {
      ...activePlot,
      pest: {
        ...activePlot.pest!,
        occurrenceId: 'occurrence-2',
      },
    },
    response,
  )

  assert.equal(matching.pest?.status, 'removed')
  assert.equal(matching.remainingYield, 3)
  assert.deepEqual(newerCycle, {})
})

test('a plot without an occurrence id is never patched by a removal response', () => {
  const legacyPlot: Plot = {
    ...activePlot,
    pest: {
      ...activePlot.pest!,
      occurrenceId: null,
    },
  }

  assert.deepEqual(
    confirmedPestRemovalPatch(
      legacyPlot,
      pestRemovalResponse(),
    ),
    {},
  )
})

test('reuses an idempotency key only for the same pest occurrence', () => {
  let generatedKeys = 0
  const createKey = () => `key-${++generatedKeys}`
  const first = getPestRemovalAttempt(
    null,
    'occurrence-1',
    createKey,
  )
  const retry = getPestRemovalAttempt(
    first,
    'occurrence-1',
    createKey,
  )
  const nextOccurrence = getPestRemovalAttempt(
    retry,
    'occurrence-2',
    createKey,
  )

  assert.equal(retry, first)
  assert.equal(retry.key, 'key-1')
  assert.equal(nextOccurrence.key, 'key-2')
  assert.equal(nextOccurrence.pestOccurrenceId, 'occurrence-2')
})

test('confirmed theft updates yield and removes only a pest cancellation confirmed by the server', () => {
  const cancelled = patchFarmPlot(
    farm,
    activePlot.id,
    plot => confirmedTheftPatch(plot, {
      plotId: activePlot.id,
      stolen: 1,
      ownerWillReceive: 2,
      xpGained: 0,
      pestCancelled: true,
    }),
  )
  const preserved = patchFarmPlot(
    farm,
    activePlot.id,
    plot => confirmedTheftPatch(plot, {
      plotId: activePlot.id,
      stolen: 1,
      ownerWillReceive: 2,
      xpGained: 0,
      pestCancelled: false,
    }),
  )

  assert.equal(cancelled.plots[0].remainingYield, 2)
  assert.equal(cancelled.plots[0].pest, null)
  assert.equal(preserved.plots[0].remainingYield, 2)
  assert.equal(preserved.plots[0].pest?.status, 'active')
})

test('only the newest farm request may commit its response', () => {
  assert.equal(
    isCurrentFarmRequest(4, 5, 'OWN:my', 'OWN:my'),
    false,
  )
  assert.equal(
    isCurrentFarmRequest(5, 5, 'OWN:my', 'OWN:my'),
    true,
  )
  assert.equal(
    isCurrentFarmRequest(
      5,
      5,
      'VISITING:old-farm',
      'VISITING:new-farm',
    ),
    false,
  )
})
