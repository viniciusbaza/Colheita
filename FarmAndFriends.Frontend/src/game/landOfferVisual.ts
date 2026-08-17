type LandOfferVisualFarm = {
  landOffer: {
    plotId: string
  } | null
  plots: readonly {
    id: string
  }[]
}

/**
 * Resolve apenas ofertas que apontam para um plot presente na topologia
 * autoritativa recebida. A cena não cria marcadores para terrenos futuros.
 */
export function resolveLandOfferPlotId(
  farm: LandOfferVisualFarm,
): string | null {
  const plotId = farm.landOffer?.plotId

  if (!plotId) return null

  return farm.plots.some(plot => plot.id === plotId) ? plotId : null
}
