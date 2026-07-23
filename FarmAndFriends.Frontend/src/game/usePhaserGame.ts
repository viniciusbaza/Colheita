import { useEffect, useRef } from 'react'
import Phaser from 'phaser'
import FarmScene from './scenes/FarmScene'
import type { Farm } from '../types/Farm'
import {
  dispatchFarmCameraMode,
  type FarmCameraMode,
} from './farmCamera'

type Props = {
  farm: Farm | null
  cameraMode: FarmCameraMode
}

export function usePhaserGame(
  containerId: string,
  { farm, cameraMode }: Props,
) {
  const gameRef = useRef<Phaser.Game | null>(null)
  const activeFarmIdRef = useRef<string | null>(null)

  useEffect(() => {
    if (gameRef.current) return

    const game = new Phaser.Game({
      type: Phaser.AUTO,
      parent: containerId,
      backgroundColor: '#dff5ff',
      transparent: false,
      scale: {
        mode: Phaser.Scale.RESIZE,
        width: '100%',
        height: '100%',
      },
      render: {
        antialias: true,
        pixelArt: false,
        roundPixels: true,
      },
      scene: [],
    })

    game.canvas.style.backgroundColor = '#dff5ff'
    gameRef.current = game

    return () => {
      gameRef.current?.destroy(true)
      gameRef.current = null
      activeFarmIdRef.current = null
    }
  }, [containerId])

  useEffect(() => {
    const game = gameRef.current
    if (!game || !farm) return

    game.registry.set('farmCameraMode', cameraMode)

    if (!game.scene.getScene('FarmScene')) {
      game.scene.add('FarmScene', FarmScene, true, { farm })
    } else if (activeFarmIdRef.current !== farm.id) {
      game.scene.start('FarmScene', { farm })
    }

    activeFarmIdRef.current = farm.id
    dispatchFarmCameraMode(cameraMode)
  }, [cameraMode, farm])
}
