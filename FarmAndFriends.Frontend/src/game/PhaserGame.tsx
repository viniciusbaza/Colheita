import { usePhaserGame } from "./usePhaserGame"
import type { Farm } from '../types/Farm'

type Props = {
  farm: Farm
}

export function PhaserGame({ farm }: Props) {
  usePhaserGame('game-container', { farm })

  return (
    <div
      id="game-container"
      className="w-full h-full flex items-center justify-center"
    />
  )
}
