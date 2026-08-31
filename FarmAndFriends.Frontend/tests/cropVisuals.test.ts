import assert from 'node:assert/strict'
import test from 'node:test'
import {
  CROP_VISUALS,
  MAX_CROP_DISPLAY_HEIGHT,
  getNextCropVisualTransitionAt,
  resolveCropVisual,
  resolvePlotVisual,
} from '../src/game/cropVisuals.ts'

const plantedAt = '2026-08-26T10:00:00.000Z'
const readyAt = '2026-08-26T12:00:00.000Z'

function at(value: string) {
  return new Date(value).getTime()
}

function plot(overrides: Record<string, unknown> = {}) {
  return {
    unlocked: true,
    seedId: 'tomato',
    plantedAt,
    readyAt,
    isReady: false,
    currentHarvestCycle: 1,
    ...overrides,
  }
}

test('uses locked and empty ground independently from crop art', () => {
  assert.deepEqual(resolvePlotVisual(plot({ seedId: null })), {
    groundTexture: 'plot',
    crop: null,
  })
  assert.deepEqual(resolvePlotVisual(plot({ unlocked: false })), {
    groundTexture: 'plot-locked',
    crop: null,
  })
})

test('switches from planted ground to sprout exactly at 25 percent', () => {
  assert.deepEqual(
    resolvePlotVisual(plot(), at('2026-08-26T10:29:59.999Z')),
    { groundTexture: 'plot-growing', crop: null },
  )

  for (const visualNow of [
    at('2026-08-26T10:30:00.000Z'),
    at('2026-08-26T10:30:00.001Z'),
  ]) {
    assert.deepEqual(resolvePlotVisual(plot(), visualNow), {
      groundTexture: 'plot',
      crop: {
        ...CROP_VISUALS.tomato.sprout,
        stage: 'sprout',
      },
    })
  }
})

test('switches from sprout to mature exactly at 50 percent', () => {
  assert.deepEqual(
    resolvePlotVisual(plot(), at('2026-08-26T10:59:59.999Z')),
    {
      groundTexture: 'plot',
      crop: {
        ...CROP_VISUALS.tomato.sprout,
        stage: 'sprout',
      },
    },
  )

  for (const visualNow of [
    at('2026-08-26T11:00:00.000Z'),
    at('2026-08-26T11:00:00.001Z'),
  ]) {
    assert.deepEqual(resolvePlotVisual(plot(), visualNow), {
      groundTexture: 'plot',
      crop: {
        ...CROP_VISUALS.tomato.mature,
        stage: 'mature',
      },
    })
  }
})

test('uses mature art during initial growth and every later regrow', () => {
  assert.deepEqual(
    resolvePlotVisual(
      plot({ seedId: 'apple_tree' }),
      at('2026-08-26T11:00:00.000Z'),
    ),
    {
      groundTexture: 'plot',
      crop: {
        ...CROP_VISUALS.apple_tree.mature,
        stage: 'mature',
      },
    },
  )

  assert.deepEqual(
    resolvePlotVisual(plot({ currentHarvestCycle: 2 }), Date.now()),
    {
      groundTexture: 'plot',
      crop: {
        ...CROP_VISUALS.tomato.mature,
        stage: 'mature',
      },
    },
  )
})

test('requires a dedicated mature visual for every configured crop', () => {
  for (const [seedId, profile] of Object.entries(CROP_VISUALS)) {
    assert.ok(profile.sprout, `${seedId} must define sprout art`)
    assert.ok(profile.mature, `${seedId} must define mature art`)
    assert.ok(profile.ready, `${seedId} must define ready art`)
    assert.match(profile.mature.texture, /^crop-mature-/)
    assert.notEqual(profile.mature.texture, profile.ready.texture)
  }
})

test('preserves each configured horizontal ground offset for every crop stage', () => {
  const sproutNow = at('2026-08-26T10:30:00.000Z')
  const matureNow = at('2026-08-26T11:00:00.000Z')

  for (const [seedId, profile] of Object.entries(CROP_VISUALS)) {
    assert.equal(
      resolveCropVisual(plot({ seedId }), sproutNow)?.groundOffsetX,
      profile.sprout.groundOffsetX,
    )
    assert.equal(
      resolveCropVisual(plot({ seedId }), matureNow)?.groundOffsetX,
      profile.mature.groundOffsetX,
    )
    assert.equal(
      resolveCropVisual(plot({ seedId, isReady: true }))?.groundOffsetX,
      profile.ready.groundOffsetX,
    )
    assert.equal(
      resolveCropVisual(plot({ seedId, currentHarvestCycle: 2 }))?.groundOffsetX,
      profile.mature.groundOffsetX,
    )
  }
})

test('compensates carrot alpha padding so visible bases stay aligned', () => {
  assert.equal(CROP_VISUALS.carrot.sprout.groundOffsetY, 8)
  assert.equal(CROP_VISUALS.carrot.mature.groundOffsetY, 6)
  assert.equal(CROP_VISUALS.carrot.ready.groundOffsetY, 9)
})

test('uses ready art only from authoritative ready state', () => {
  assert.equal(
    resolveCropVisual(plot(), at('2026-08-26T12:30:00.000Z'))?.stage,
    'mature',
  )
  assert.equal(
    resolveCropVisual(plot({ isReady: true }), Date.now())?.texture,
    'crop-ready-tomato',
  )
  assert.equal(
    resolveCropVisual(
      plot({ seedId: 'apple_tree', isReady: true, currentHarvestCycle: 3 }),
      Date.now(),
    )?.texture,
    'crop-ready-apple-tree',
  )
})

test('keeps the apple tree visibly taller than conventional crops', () => {
  const appleTree = resolveCropVisual(
    plot({ seedId: 'apple_tree', isReady: true }),
    Date.now(),
  )
  const corn = resolveCropVisual(
    plot({ seedId: 'corn', isReady: true }),
    Date.now(),
  )

  assert.ok(appleTree)
  assert.ok(corn)
  assert.ok(appleTree.displayHeight >= corn.displayHeight * 1.5)
  assert.equal(MAX_CROP_DISPLAY_HEIGHT, appleTree.displayHeight)
})

test('fails visually safe for an unknown crop through every stage', () => {
  const unknown = plot({ seedId: 'future_crop' })

  assert.deepEqual(
    resolvePlotVisual(unknown, at('2026-08-26T10:29:59.999Z')),
    { groundTexture: 'plot-growing', crop: null },
  )
  assert.deepEqual(
    resolvePlotVisual(unknown, at('2026-08-26T10:30:00.000Z')),
    { groundTexture: 'plot', crop: null },
  )
  assert.deepEqual(
    resolvePlotVisual(unknown, at('2026-08-26T11:00:00.000Z')),
    { groundTexture: 'plot', crop: null },
  )
  assert.deepEqual(
    resolvePlotVisual(unknown, Date.now()),
    { groundTexture: 'plot', crop: null },
  )
})

test('schedules only the next 25 or 50 percent initial-growth boundary', () => {
  assert.equal(
    getNextCropVisualTransitionAt(
      plot(),
      at('2026-08-26T10:29:59.999Z'),
    ),
    at('2026-08-26T10:30:00.000Z'),
  )
  assert.equal(
    getNextCropVisualTransitionAt(
      plot(),
      at('2026-08-26T10:30:00.000Z'),
    ),
    at('2026-08-26T11:00:00.000Z'),
  )
  assert.equal(
    getNextCropVisualTransitionAt(
      plot(),
      at('2026-08-26T10:59:59.999Z'),
    ),
    at('2026-08-26T11:00:00.000Z'),
  )
  assert.equal(
    getNextCropVisualTransitionAt(
      plot(),
      at('2026-08-26T11:00:00.000Z'),
    ),
    null,
  )
})

test('does not schedule visual boundaries outside active initial growth', () => {
  const now = at('2026-08-26T10:15:00.000Z')

  assert.equal(getNextCropVisualTransitionAt(plot({ seedId: null }), now), null)
  assert.equal(getNextCropVisualTransitionAt(plot({ unlocked: false }), now), null)
  assert.equal(getNextCropVisualTransitionAt(plot({ isReady: true }), now), null)
  assert.equal(
    getNextCropVisualTransitionAt(plot({ currentHarvestCycle: 2 }), now),
    null,
  )
  assert.equal(
    getNextCropVisualTransitionAt(plot({ readyAt: 'invalid' }), now),
    null,
  )
})
