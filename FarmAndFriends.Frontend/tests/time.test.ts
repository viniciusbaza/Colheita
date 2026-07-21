import assert from 'node:assert/strict'
import test from 'node:test'
import { getNextPlotReadyAt } from '../src/utils/time.ts'

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
