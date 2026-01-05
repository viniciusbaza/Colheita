import { usePhaserGame } from "./usePhaserGame"

export function PhaserGame() {
  usePhaserGame('game-container')

  return (
    <div
      id="game-container"
      className="w-full h-full flex items-center justify-center"
    />
  )
}
