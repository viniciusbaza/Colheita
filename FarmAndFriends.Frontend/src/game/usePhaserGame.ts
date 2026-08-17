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
  isVisiting: boolean
}

export function usePhaserGame(
  containerId: string,
  { farm, cameraMode, isVisiting }: Props,
) {
  const gameRef = useRef<Phaser.Game | null>(null)
  const activeFarmViewRef = useRef<string | null>(null)
  const teardownTimerRef = useRef<number | null>(null)

  useEffect(() => {
    // StrictMode immediately runs cleanup + setup again in development.
    // Cancel that pending teardown so both setups keep one canvas and game.
    if (teardownTimerRef.current !== null) {
      window.clearTimeout(teardownTimerRef.current)
      teardownTimerRef.current = null
    }

    if (!gameRef.current) {
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
      game.canvas.style.display = 'block'
      gameRef.current = game
    }

    const game = gameRef.current

    return () => {
      // A real unmount has no following setup to cancel this task.
      teardownTimerRef.current = window.setTimeout(() => {
        if (gameRef.current !== game) return

        gameRef.current = null
        activeFarmViewRef.current = null
        teardownTimerRef.current = null
        game.destroy(true)
        game.canvas.remove()
      }, 0)
    }
  }, [containerId])

  useEffect(() => {
    const game = gameRef.current
    if (!game || !farm) return

    game.registry.set('farmCameraMode', cameraMode)

    // SceneManager may still have the first add queued when StrictMode runs
    // this effect again, so the active farm is the idempotency guard.
    const farmViewKey = `${farm.id}:${isVisiting ? 'visitor' : 'owner'}`

    if (activeFarmViewRef.current !== farmViewKey) {
      if (!game.scene.getScene('FarmScene')) {
        game.scene.add('FarmScene', FarmScene, true, { farm, isVisiting })
      } else {
        game.scene.start('FarmScene', { farm, isVisiting })
      }

      activeFarmViewRef.current = farmViewKey
    }

    dispatchFarmCameraMode(cameraMode)
  }, [cameraMode, farm, isVisiting])
}
