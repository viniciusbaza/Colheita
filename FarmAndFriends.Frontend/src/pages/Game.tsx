import { useUser } from '../user/useUser'
import { useFarm } from '../farm/FarmContext'
import { usePlotInteraction } from '../farm/usePlotInteraction'
import { PlayerHUD } from '../components/PlayerHUD' 
import { PhaserGame } from '../game/PhaserGame'
import { PlotModal } from '../components/PlotModal'
import { clamp } from '../utils/math'
import { useEffect } from 'react'

export default function Game() {
  const { user, loading } = useUser()
  const { farm, loading: farmLoading } = useFarm()
  const { selectedPlot, closePlot } = usePlotInteraction()

  useEffect(() => {
    window.dispatchEvent(
      new CustomEvent('ui:modal', {
        detail: { source: 'plot', open: !!selectedPlot }
      })
    )
  }, [selectedPlot])
  
  // Estado de loading
  if (loading || farmLoading) {
    return (
      <div className="h-screen flex items-center justify-center bg-black text-white">
        <p>🔄 Carregando jogador...</p>
        <p>🌱 Carregando fazenda...</p>
      </div>
    )
  }

  // Estado de erro
  if (!user) {
    return (
      <div className="h-screen flex items-center justify-center bg-red-900 text-white">
        <p>❌ Erro ao carregar jogador</p>
      </div>
    )
  }
  
  if (!farm) {
    return (
      <div className="h-screen flex items-center justify-center bg-red-900 text-white">
        <p>❌ Erro ao carregar fazenda</p>
      </div>
    )
  }

  // Estado autenticado
  return (
    <div className="w-screen h-screen overflow-hidden bg-green-100 text-white">

      {/* Phaser ocupa todo o espaço */}
      <PhaserGame farm={farm} />

      {/* HUD flutuante */}
      <PlayerHUD />

      {/* Modal do Plot */}
      {selectedPlot && (() => {
        const MARGIN = 16
        
        const modalX = clamp(
          selectedPlot.x,
          MARGIN,
          window.innerWidth - MARGIN
        )

        const modalY = clamp(
          selectedPlot.y,
          MARGIN + 40,
          window.innerHeight - MARGIN
        )

        return (
          <div className='fixed inset-0 z-40' onClick={closePlot}>
            <div className='fixed z-50'
              style={{
                left: modalX,
                top: modalY,
                transform: 'translate(-50%, -100%)',
              }}
              onClick={(e) => e.stopPropagation()}
            >
              <PlotModal 
                plotId={selectedPlot.plotId} 
                onClose={closePlot} 
              />
            </div>
          </div>
        )
      })()}
    </div>
  )
}
