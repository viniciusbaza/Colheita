import { useEffect, useRef } from 'react'
import Phaser from 'phaser'
import FarmScene from './scenes/FarmScene'
import type { Farm } from '../types/Farm'

type Props = {
  farm: Farm | null
}

export function usePhaserGame(containerId: string, { farm }: Props) {
  const gameRef = useRef<Phaser.Game | null>(null)
  const activeFarmIdRef = useRef<string | null>(null)

  useEffect(() => {
    if (gameRef.current) return

    gameRef.current = new Phaser.Game({
      type: Phaser.AUTO,
      parent: containerId,
      width: window.innerWidth,
      height: window.innerHeight,
      backgroundColor: '#87CEEB',
      scene: [],
    })

    return () => {
      gameRef.current?.destroy(true)
      gameRef.current = null
      activeFarmIdRef.current = null
    }
  }, [containerId])

  useEffect(() => {
    const game = gameRef.current
    if (!game || !farm) return

    if (!game.scene.getScene('FarmScene')) {
      game.scene.add('FarmScene', FarmScene, true, { farm })
    } else if (activeFarmIdRef.current !== farm.id) {
      game.scene.start('FarmScene', { farm })
    }

    activeFarmIdRef.current = farm.id
  }, [farm])
}
