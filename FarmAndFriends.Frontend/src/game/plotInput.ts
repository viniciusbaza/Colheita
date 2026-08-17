type PlotInputState = {
  unlocked: boolean
}

export function canReceivePlotInput(
  plot: PlotInputState,
  isVisiting: boolean,
) {
  return plot.unlocked || !isVisiting
}
