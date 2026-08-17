import assert from 'node:assert/strict'
import test from 'node:test'
import { resolveLandOfferPlotId } from '../src/game/landOfferVisual.ts'

const plots = Array.from({ length: 28 }, (_, index) => ({
  id: `plot-${index + 1}`,
}))

test('resolves the offered plot only when it exists in the current topology', () => {
  assert.equal(
    resolveLandOfferPlotId({
      landOffer: { plotId: 'plot-10' },
      plots,
    }),
    'plot-10',
  )
  assert.equal(
    resolveLandOfferPlotId({
      landOffer: { plotId: 'plot-29' },
      plots,
    }),
    null,
  )
})

test('does not expose a marker after completion or while visiting', () => {
  assert.equal(resolveLandOfferPlotId({ landOffer: null, plots }), null)
})
