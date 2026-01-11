import { useUser } from '../user/useUser'
import { useFarm } from '../farm/useFarm'
import { usePlotInteraction } from '../farm/usePlotInteraction'
import { PlayerHUD } from '../components/PlayerHUD' 
import { PhaserGame } from '../game/PhaserGame'
import { PlotModal } from '../components/PlotModal'
import { clamp } from '../utils/math'

export default function Game() {
  const { user, loading } = useUser()
  const { farm } = useFarm()
  const { selectedPlot, closePlot } = usePlotInteraction()
  
  // Estado de loading
  if (loading) {
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
        const modalX = clamp(selectedPlot.x, 120, window.innerWidth - 120)
        const modalY = clamp(selectedPlot.y, 140, window.innerHeight - 40)

        return (
          <div
            className='absolute z-50'
            style={{
              left: modalX,
              top: modalY,
              transform: 'translate(-50%, -100%)',
            }}
          >
            <PlotModal plotId={selectedPlot.plotId} onClose={closePlot} />
          </div>
        )
      })()}
    </div>
  )
}
