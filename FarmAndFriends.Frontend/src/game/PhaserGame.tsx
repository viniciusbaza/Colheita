import { usePhaserGame } from './usePhaserGame'
import type { Farm } from '../types/Farm'
import type { FarmCameraMode } from './farmCamera'

type Props = {
  farm: Farm
  cameraMode: FarmCameraMode
  isVisiting: boolean
}

export function PhaserGame({ farm, cameraMode, isVisiting }: Props) {
  usePhaserGame('game-container', { farm, cameraMode, isVisiting })

  return (
    <div
      id="game-container"
      className="absolute inset-0 overflow-hidden bg-[#dff5ff]"
    />
  )
}
