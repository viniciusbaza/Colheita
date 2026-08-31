import type {
  Farm,
  HarvestRequest,
  HarvestResponse,
  PestActionResponse,
  Plot,
  StealResponse,
} from '../types/Farm'

export type ConfirmedPlotPatch = Partial<
  Pick<
    Plot,
    | 'care'
    | 'currentHarvestCycle'
    | 'isReady'
    | 'pest'
    | 'plantedAt'
    | 'protectedUntil'
    | 'readyAt'
    | 'remainingYield'
    | 'seedId'
    | 'state'
  >
>

export type ConfirmedPlotPatchFactory = (
  plot: Plot,
) => ConfirmedPlotPatch

export type PestRemovalAttempt = {
  pestOccurrenceId: string
  key: string
}

export function patchFarmPlot(
  farm: Farm,
  plotId: string,
  patchOrFactory: ConfirmedPlotPatch | ConfirmedPlotPatchFactory,
) {
  const plotIndex = farm.plots.findIndex(plot => plot.id === plotId)
  if (plotIndex < 0) return farm

  const currentPlot = farm.plots[plotIndex]
  const patch = typeof patchOrFactory === 'function'
    ? patchOrFactory(currentPlot)
    : patchOrFactory
  const plots = [...farm.plots]
  plots[plotIndex] = { ...currentPlot, ...patch }

  return { ...farm, plots }
}

export function confirmedTheftPatch(
  plot: Plot,
  response: StealResponse,
): ConfirmedPlotPatch {
  return {
    remainingYield: response.ownerWillReceive,
    pest: response.pestCancelled ? null : plot.pest,
  }
}

export function confirmedHarvestPatch(
  response: HarvestResponse,
): ConfirmedPlotPatch {
  if (response.currentHarvestCycle === null) {
    return {
      care: null,
      currentHarvestCycle: null,
      isReady: false,
      pest: null,
      plantedAt: null,
      readyAt: null,
      remainingYield: null,
      seedId: null,
      state: 'empty',
    }
  }

  return {
    care: null,
    currentHarvestCycle: response.currentHarvestCycle,
    isReady: false,
    pest: null,
    readyAt: response.readyAt,
    remainingYield: null,
    state: 'growing',
  }
}

export function getHarvestCyclePrecondition(
  plot: Pick<Plot, 'currentHarvestCycle'>,
): HarvestRequest | null {
  return plot.currentHarvestCycle === null
    ? null
    : { expectedHarvestCycle: plot.currentHarvestCycle }
}

export function confirmedPestRemovalPatch(
  plot: Plot,
  response: PestActionResponse,
): ConfirmedPlotPatch {
  const matchesCurrentOccurrence = (
    plot.pest?.occurrenceId === response.pestOccurrenceId
  )

  if (!matchesCurrentOccurrence) {
    return {}
  }

  return {
    pest: response.pest,
    remainingYield: response.remainingYield,
  }
}

export function getPestRemovalAttempt(
  currentAttempt: PestRemovalAttempt | null,
  pestOccurrenceId: string,
  createKey: () => string,
): PestRemovalAttempt {
  if (currentAttempt?.pestOccurrenceId === pestOccurrenceId) {
    return currentAttempt
  }

  return {
    pestOccurrenceId,
    key: createKey(),
  }
}

export function isCurrentFarmRequest(
  requestId: number,
  latestRequestId: number,
  requestSessionKey: string,
  activeSessionKey: string,
) {
  return (
    requestId === latestRequestId
    && requestSessionKey === activeSessionKey
  )
}
