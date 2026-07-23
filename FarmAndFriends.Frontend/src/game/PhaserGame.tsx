import { usePhaserGame } from './usePhaserGame'
import type { Farm } from '../types/Farm'
import type { FarmCameraMode } from './farmCamera'

type Props = {
  farm: Farm
  cameraMode: FarmCameraMode
}

export function PhaserGame({ farm, cameraMode }: Props) {
  usePhaserGame('game-container', { farm, cameraMode })

  return (
    <div
      id="game-container"
      className="absolute inset-0 overflow-hidden bg-[#dff5ff]"
    />
  )
}
