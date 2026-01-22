import { useEffect, useRef } from 'react'
import Phaser from 'phaser'
import FarmScene from './scenes/FarmScene'
import type { Farm } from '../types/Farm'

type Props = {
  farm: Farm | null
}

export function usePhaserGame(containerId: string, { farm }: Props) {
  const gameRef = useRef<Phaser.Game | null>(null)

  // 1️⃣ Cria o Phaser Game UMA VEZ
  useEffect(() => {
    if (gameRef.current) return

    gameRef.current = new Phaser.Game({
      type: Phaser.AUTO,
      parent: containerId,
      width: window.innerWidth,
      height: window.innerHeight,
      backgroundColor: '#87CEEB',
      scene: []
    })

    return () => {
      gameRef.current?.destroy(true)
      gameRef.current = null
    }
  }, [containerId])
  
  // 2️⃣ Recria a FarmScene quando a farm muda
  useEffect(() => {
    function onFarmChange(e: Event) {
      const farm = (e as CustomEvent<Farm>).detail
      const game = gameRef.current
      if (!game) return

      // 💥 força reinício visual
      if (game.scene.getScene('FarmScene')) {
        game.scene.start('FarmScene', { farm })
      } else {
        game.scene.add('FarmScene', FarmScene, true, { farm })
      }
    }

    window.addEventListener('farm:change', onFarmChange)
    return () => {
      window.removeEventListener('farm:change', onFarmChange)
    }
  }, [])
}
