import assert from 'node:assert/strict'
import test from 'node:test'
import {
  formatCountdown,
  formatTimeRemaining,
  formatTimeSpanDuration,
  getNextCareAt,
  getNextFarmStateAt,
  getNextPestOrProtectionAt,
  getNextPlotReadyAt,
  parseTimeSpanToSeconds,
} from '../src/utils/time.ts'

test('formats crop deadlines with compact seconds, minutes, and hours', () => {
  const now = Date.parse('2026-08-23T10:00:00.000Z')

  assert.equal(
    formatTimeRemaining('2026-08-23T10:00:42.000Z', now),
    '42s',
  )
  assert.equal(
    formatTimeRemaining('2026-08-23T10:08:42.000Z', now),
    '8m 42s',
  )
  assert.equal(
    formatTimeRemaining('2026-08-23T11:08:42.000Z', now),
    '1h 8m',
  )
  assert.equal(
    formatTimeRemaining('2026-08-23T09:59:59.000Z', now),
    '✅',
  )
})

test('parses .NET durations with and without a day component', () => {
  assert.equal(parseTimeSpanToSeconds('02:30:00'), 9_000)
  assert.equal(parseTimeSpanToSeconds('2.03:04:05'), 183_845)
  assert.equal(parseTimeSpanToSeconds('1.00:00:00.5000000'), 86_400)
})

test('rejects malformed .NET durations instead of returning a partial value', () => {
  assert.equal(Number.isNaN(parseTimeSpanToSeconds('2 days')), true)
  assert.equal(Number.isNaN(parseTimeSpanToSeconds('1.24:00:00')), true)
  assert.equal(Number.isNaN(parseTimeSpanToSeconds('00:60:00')), true)
})

test('formats seed durations for compact shop metadata', () => {
  assert.equal(formatTimeSpanDuration('02:00:00'), '2h')
  assert.equal(formatTimeSpanDuration('1.12:30:00'), '1 dia 12h 30min')
  assert.equal(formatTimeSpanDuration('invalid'), '—')
})

test('formats display-only pest and protection countdowns', () => {
  const now = Date.parse('2026-07-30T10:00:00.000Z')

  assert.equal(
    formatCountdown('2026-07-30T11:34:20.000Z', now),
    '01:34:20',
  )
  assert.equal(
    formatCountdown('2026-07-30T10:08:42.000Z', now),
    '08:42',
  )
  assert.equal(
    formatCountdown('2026-07-30T09:00:00.000Z', now),
    '00:00',
  )
  assert.equal(formatCountdown('invalid-date', now), null)
})

test('returns the nearest pending plot deadline', () => {
  const nextReadyAt = getNextPlotReadyAt([
    {
      seedId: 'tomato',
      isReady: false,
      readyAt: '2026-07-20T15:05:00.000Z',
    },
    {
      seedId: 'carrot',
      isReady: false,
      readyAt: '2026-07-20T15:02:00.000Z',
    },
  ])

  assert.equal(nextReadyAt, Date.parse('2026-07-20T15:02:00.000Z'))
})

test('ignores empty, ready, and invalid plots', () => {
  const nextReadyAt = getNextPlotReadyAt([
    {
      seedId: null,
      isReady: false,
      readyAt: '2026-07-20T15:01:00.000Z',
    },
    {
      seedId: 'corn',
      isReady: true,
      readyAt: '2026-07-20T15:02:00.000Z',
    },
    {
      seedId: 'tomato',
      isReady: false,
      readyAt: 'invalid-date',
    },
  ])

  assert.equal(nextReadyAt, null)
})

test('detects a new deadline after planting in an empty farm', () => {
  assert.equal(getNextPlotReadyAt([]), null)

  const readyAt = '2026-07-20T15:03:00.000Z'
  const nextReadyAt = getNextPlotReadyAt([
    {
      seedId: 'carrot',
      isReady: false,
      readyAt,
    },
  ])

  assert.equal(nextReadyAt, Date.parse(readyAt))
})

test('returns the nearest server-provided care cooldown deadline', () => {
  const now = Date.parse('2026-07-20T10:00:00.000Z')
  const nextCareAt = getNextCareAt([
    {
      seedId: 'corn',
      isReady: false,
      readyAt: '2026-07-27T10:00:00.000Z',
      care: {
        canCare: false,
        rewardAvailable: false,
        nextCareAt: '2026-07-20T15:00:00.000Z',
      },
    },
    {
      seedId: 'tomato',
      isReady: false,
      readyAt: '2026-07-27T10:00:00.000Z',
      care: {
        canCare: false,
        rewardAvailable: false,
        nextCareAt: '2026-07-20T14:00:00.000Z',
      },
    },
  ], now)

  assert.equal(nextCareAt, Date.parse('2026-07-20T14:00:00.000Z'))
})

test('schedules the end of a care cycle that currently has no reward', () => {
  const now = Date.parse('2026-07-20T10:00:00.000Z')
  const nextCareAt = getNextCareAt([
    {
      seedId: 'corn',
      isReady: false,
      readyAt: '2026-07-27T10:00:00.000Z',
      care: {
        canCare: true,
        rewardAvailable: false,
        nextCareAt: null,
        careCycleEndsAt: '2026-07-20T12:00:00.000Z',
      },
    },
  ], now)

  assert.equal(nextCareAt, Date.parse('2026-07-20T12:00:00.000Z'))
})

test('returns the nearest cooldown or care-cycle deadline', () => {
  const now = Date.parse('2026-07-20T10:00:00.000Z')
  const nextCareAt = getNextCareAt([
    {
      seedId: 'corn',
      isReady: false,
      readyAt: '2026-07-27T10:00:00.000Z',
      care: {
        canCare: false,
        rewardAvailable: false,
        nextCareAt: '2026-07-20T15:00:00.000Z',
        careCycleEndsAt: null,
      },
    },
    {
      seedId: 'tomato',
      isReady: false,
      readyAt: '2026-07-27T10:00:00.000Z',
      care: {
        canCare: true,
        rewardAvailable: false,
        nextCareAt: null,
        careCycleEndsAt: '2026-07-20T14:00:00.000Z',
      },
    },
  ], now)

  assert.equal(nextCareAt, Date.parse('2026-07-20T14:00:00.000Z'))
})

test('does not schedule care already available or after crop maturity', () => {
  const now = Date.parse('2026-07-20T10:00:00.000Z')
  const nextCareAt = getNextCareAt([
    {
      seedId: 'corn',
      isReady: false,
      readyAt: '2026-07-20T16:00:00.000Z',
      care: {
        canCare: true,
        rewardAvailable: true,
        nextCareAt: null,
      },
    },
    {
      seedId: 'tomato',
      isReady: false,
      readyAt: '2026-07-20T14:00:00.000Z',
      care: {
        canCare: false,
        rewardAvailable: false,
        nextCareAt: '2026-07-20T15:00:00.000Z',
      },
    },
    {
      seedId: 'carrot',
      isReady: false,
      readyAt: '2026-07-20T14:00:00.000Z',
      care: {
        canCare: true,
        rewardAvailable: false,
        careCycleEndsAt: '2026-07-20T15:00:00.000Z',
      },
    },
  ], now)

  assert.equal(nextCareAt, null)
})

test('past care deadlines never enable care locally', () => {
  const now = Date.parse('2026-07-20T15:00:00.000Z')
  const nextCareAt = getNextCareAt([
    {
      seedId: 'corn',
      isReady: false,
      readyAt: '2026-07-27T10:00:00.000Z',
      care: {
        canCare: false,
        rewardAvailable: false,
        nextCareAt: '2026-07-20T14:59:59.000Z',
      },
    },
    {
      seedId: 'tomato',
      isReady: false,
      readyAt: '2026-07-27T10:00:00.000Z',
      care: {
        canCare: true,
        rewardAvailable: false,
        careCycleEndsAt: '2026-07-20T14:59:59.000Z',
      },
    },
  ], now)

  assert.equal(nextCareAt, null)
})

test('returns the nearest scheduled appearance, active consumption, or protection expiry', () => {
  const now = Date.parse('2026-07-30T10:00:00.000Z')
  const nextPestAt = getNextPestOrProtectionAt([
    {
      pest: {
        status: 'scheduled',
        appearsAt: '2026-07-30T10:15:00.000Z',
      },
    },
    {
      pest: {
        status: 'active',
        consumesAt: '2026-07-30T10:12:00.000Z',
      },
    },
    {
      protectedUntil: '2026-07-30T10:08:00.000Z',
    },
  ], now)

  assert.equal(nextPestAt, Date.parse('2026-07-30T10:08:00.000Z'))
})

test('ignores resolved pests, unrelated pest timestamps, and expired deadlines', () => {
  const now = Date.parse('2026-07-30T10:00:00.000Z')
  const nextPestAt = getNextPestOrProtectionAt([
    {
      protectedUntil: '2026-07-30T09:59:00.000Z',
      pest: {
        status: 'consumed',
        consumesAt: '2026-07-30T10:15:00.000Z',
      },
    },
    {
      pest: {
        status: 'active',
        appearsAt: '2026-07-30T10:05:00.000Z',
        consumesAt: 'invalid-date',
      },
    },
  ], now)

  assert.equal(nextPestAt, null)
})

test('combines ready, care, pest, and protection into one authoritative refresh deadline', () => {
  const now = Date.parse('2026-07-30T10:00:00.000Z')
  const plots = [
    {
      seedId: 'corn',
      isReady: false,
      readyAt: '2026-07-30T10:20:00.000Z',
      protectedUntil: '2026-07-30T10:18:00.000Z',
      care: {
        canCare: false,
        rewardAvailable: false,
        nextCareAt: '2026-07-30T10:10:00.000Z',
      },
      pest: null,
    },
    {
      seedId: 'tomato',
      isReady: true,
      readyAt: '2026-07-30T09:00:00.000Z',
      protectedUntil: null,
      care: null,
      pest: {
        status: 'active',
        consumesAt: '2026-07-30T10:05:00.000Z',
      },
    },
  ]

  assert.equal(
    getNextFarmStateAt(plots, true, null, now),
    Date.parse('2026-07-30T10:05:00.000Z'),
  )
})

test('does not use visitor-only care deadlines on the owner farm', () => {
  const now = Date.parse('2026-07-30T10:00:00.000Z')
  const plots = [{
    seedId: 'corn',
    isReady: false,
    readyAt: '2026-07-30T10:20:00.000Z',
    protectedUntil: '2026-07-30T10:15:00.000Z',
    care: {
      canCare: false,
      rewardAvailable: false,
      nextCareAt: '2026-07-30T10:05:00.000Z',
    },
    pest: null,
  }]

  assert.equal(
    getNextFarmStateAt(plots, false, null, now),
    Date.parse('2026-07-30T10:15:00.000Z'),
  )
})

test('includes the server-provided next pest evaluation without deriving safety locally', () => {
  const now = Date.parse('2026-07-30T10:00:00.000Z')
  const nextPestCheckAt = '2026-07-30T10:03:00.000Z'

  assert.equal(
    getNextFarmStateAt([], false, nextPestCheckAt, now),
    Date.parse(nextPestCheckAt),
  )
})

test('ignores an invalid or elapsed server pest evaluation deadline', () => {
  const now = Date.parse('2026-07-30T10:00:00.000Z')

  assert.equal(
    getNextFarmStateAt([], false, 'invalid-date', now),
    null,
  )
  assert.equal(
    getNextFarmStateAt([], false, '2026-07-30T09:59:59.000Z', now),
    null,
  )
})
