import { useUser } from '../user/useUser'
import { useFarm } from '../farm/useFarmContext'
import { usePlotInteraction } from '../farm/usePlotInteraction'
import { PlayerHUD } from '../components/PlayerHUD' 
import { PhaserGame } from '../game/PhaserGame'
import { PlotModal } from '../components/PlotModal'
import type { LandPurchaseFeedback } from '../components/PlotModal'
import type { LandPurchaseAttempt } from '../land/landPurchase'
import { clamp } from '../utils/math'
import { useEffect, useRef, useState } from 'react'
import type { FarmCameraMode } from '../game/farmCamera'

export default function Game() {
  const { user, loading } = useUser()
  const {
    farm,
    loading: farmLoading,
    isVisiting,
    visitRecoveryFeedback,
    clearVisitRecoveryFeedback,
  } = useFarm()
  const { selectedPlot, closePlot } = usePlotInteraction()
  const [plotModalBusy, setPlotModalBusy] = useState(false)
  const landPurchaseAttemptStore = useRef<LandPurchaseAttempt | null>(null)
  const [landPurchaseFeedback, setLandPurchaseFeedback] = useState<
    LandPurchaseFeedback | null
  >(null)
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

  useEffect(() => {
    if (!landPurchaseFeedback) return
    if (landPurchaseFeedback.type === 'pending') return

    const timeout = window.setTimeout(() => {
      setLandPurchaseFeedback(null)
    }, landPurchaseFeedback.type === 'error' ? 8_000 : 5_000)

    return () => window.clearTimeout(timeout)
  }, [landPurchaseFeedback])
  
  // Estado de loading
  if (loading || farmLoading) {
    return (
      <div className="h-screen flex items-center justify-center bg-[#dff5ff] text-emerald-950">
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
      <PhaserGame
        farm={farm}
        cameraMode={cameraMode}
        isVisiting={isVisiting}
      />

      {/* HUD flutuante */}
      <PlayerHUD
        cameraMode={cameraMode}
        onCameraModeChange={changeCameraMode}
      />

      {visitRecoveryFeedback && (
        <div
          role="alert"
          aria-live="assertive"
          className="fixed left-1/2 top-24 z-[70] flex w-[min(28rem,calc(100vw-2rem))] -translate-x-1/2 items-start gap-3 rounded-xl border border-amber-300 bg-amber-50 px-4 py-3 text-sm font-semibold text-amber-950 shadow-lg"
        >
          <span aria-hidden="true">🏡</span>
          <p className="min-w-0 flex-1">{visitRecoveryFeedback.message}</p>
          <button
            type="button"
            onClick={clearVisitRecoveryFeedback}
            aria-label="Fechar aviso de visita"
            className="-m-2 rounded-lg p-2 text-lg leading-none hover:bg-amber-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber-800"
          >
            ×
          </button>
        </div>
      )}

      {landPurchaseFeedback && (
        <div
          role={landPurchaseFeedback.type === 'error' ? 'alert' : 'status'}
          aria-live={landPurchaseFeedback.type === 'error' ? 'assertive' : 'polite'}
          className={`pointer-events-none fixed bottom-5 left-1/2 z-[70] w-[min(24rem,calc(100vw-2rem))] -translate-x-1/2 rounded-xl border px-4 py-3 text-center text-sm font-bold shadow-lg ${
            landPurchaseFeedback.type === 'error'
              ? 'border-red-300 bg-red-50 text-red-900'
              : landPurchaseFeedback.type === 'pending'
                ? 'border-amber-300 bg-amber-50 text-amber-900'
                : 'border-emerald-300 bg-emerald-50 text-emerald-900'
          }`}
        >
          {landPurchaseFeedback.type === 'pending' ? '🔄' : '🌱'}{' '}
          {landPurchaseFeedback.message}
        </div>
      )}

      {/* Modal do Plot */}
      {selectedPlot && (() => {
        const MARGIN = 16
        const horizontalSafeMargin = Math.min(
          window.innerWidth / 2,
          160,
        )
        
        const modalX = clamp(
          selectedPlot.x,
          horizontalSafeMargin,
          Math.max(horizontalSafeMargin, window.innerWidth - horizontalSafeMargin)
        )

        const modalY = clamp(
          selectedPlot.y,
          MARGIN + 40,
          window.innerHeight - MARGIN
        )

        return (
          <div
            className='fixed inset-0 z-40'
            onClick={() => {
              if (!plotModalBusy) closePlot()
            }}
          >
            <div className='plot-modal-anchor fixed z-50'
              style={{
                left: modalX,
                top: modalY,
              }}
              onClick={(e) => e.stopPropagation()}
            >
              <PlotModal 
                plotId={selectedPlot.plotId} 
                onClose={closePlot}
                landPurchaseAttemptStore={landPurchaseAttemptStore}
                onBusyChange={setPlotModalBusy}
                onLandPurchaseFeedback={setLandPurchaseFeedback}
              />
            </div>
          </div>
        )
      })()}
    </div>
  )
}
