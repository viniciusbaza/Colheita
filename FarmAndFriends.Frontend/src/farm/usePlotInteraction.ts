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
// voltar aqui para adicionar a função openPlot, que será usada para abrir o modal do lote quando o jogador clicar em um lote específico na fazenda. A função deve receber o ID do lote e as coordenadas x e y da posição do clique, e atualizar o estado selectedPlot com essas informações.
  function openPlot(plotId: string, x?: number, y?: number) {
    setSelectedPlot({
      plotId,
      x: x ?? window.innerWidth / 2,
      y: y ?? window.innerHeight - 16,
    })
  }

  return {
    selectedPlot,
    closePlot,
    openPlot,
  }
}
