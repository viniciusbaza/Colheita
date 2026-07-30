import assert from 'node:assert/strict'
import test from 'node:test'
import {
  getNextCareAt,
  getNextPlotReadyAt,
} from '../src/utils/time.ts'

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
