import { useEffect, useState } from 'react'

export function usePlotInteraction() {
  const [selectedPlot, setSelectedPlot] = useState<{ plotId: string; x: number; y: number } | null>(null)

  useEffect(() => {
    function handlePlotClick(e: Event) {
      const customEvent = e as CustomEvent<
        { plotId: string; x: number; y: number }
      >
      setSelectedPlot(customEvent.detail)
    }

    window.addEventListener('plot:click', handlePlotClick)

    return () => {
      window.removeEventListener('plot:click', handlePlotClick)
    }
  }, [])

  function closePlot() {
    setSelectedPlot(null)
  }

  return {
    selectedPlot,
    closePlot,
  }
}
