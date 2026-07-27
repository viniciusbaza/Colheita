import { useUser } from '../user/useUser'
import { useFarm } from '../farm/useFarmContext'
import { usePlotInteraction } from '../farm/usePlotInteraction'
import { PlayerHUD } from '../components/PlayerHUD' 
import { PhaserGame } from '../game/PhaserGame'
import { PlotModal } from '../components/PlotModal'
import { clamp } from '../utils/math'
import { useEffect, useState } from 'react'
import type { FarmCameraMode } from '../game/farmCamera'

export default function Game() {
  const { user, loading } = useUser()
  const { farm, loading: farmLoading } = useFarm()
  const { selectedPlot, closePlot } = usePlotInteraction()
  const [cameraPreference, setCameraPreference] = useState<{
    farmId: string | null
    mode: FarmCameraMode
  }>({ farmId: null, mode: 'focus' })

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

  const farmId = farm.id
  const cameraMode =
    cameraPreference.farmId === farmId
      ? cameraPreference.mode
      : 'focus'

  function changeCameraMode(mode: FarmCameraMode) {
    closePlot()
    setCameraPreference({ farmId, mode })
  }

  // Estado autenticado
  return (
    <div className="relative h-screen w-screen overflow-hidden bg-[#dff5ff] text-white">

      {/* Phaser ocupa todo o espaço */}
      <PhaserGame farm={farm} cameraMode={cameraMode} />

      {/* HUD flutuante */}
      <PlayerHUD
        cameraMode={cameraMode}
        onCameraModeChange={changeCameraMode}
      />

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
