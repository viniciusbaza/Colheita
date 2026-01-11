import { useEffect, useRef } from 'react'
import Phaser from 'phaser'
import FarmScene from './scenes/FarmScene'
import type { Farm } from '../types/Farm'

type Props = {
  farm: Farm
}

export function usePhaserGame(containerId: string, { farm }: Props) {
  const gameRef = useRef<Phaser.Game | null>(null)

  useEffect(() => {
    if (gameRef.current) return

    gameRef.current = new Phaser.Game({
      type: Phaser.AUTO,
      parent: containerId,
      width: window.innerWidth,
      height: window.innerHeight,
      backgroundColor: '#87CEEB',
      scene: new FarmScene(farm),
    })

    return () => {
      gameRef.current?.destroy(true)
      gameRef.current = null
    }
  }, [containerId])
}
