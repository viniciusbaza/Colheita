import { useEffect, useRef } from 'react'
import Phaser from 'phaser'

class MainScene extends Phaser.Scene {
  constructor() {
    super('MainScene')
  }

  create() {
    this.add.text(50, 50, '🌾 Farm And Friends', {
      color: '#ffffff',
      fontSize: '24px',
    })

    this.add.text(50, 90, 'Phaser integrado com React!', {
      color: '#ffffaa',
    })
  }
}

export function usePhaserGame(containerId: string) {
  const gameRef = useRef<Phaser.Game | null>(null)

  useEffect(() => {
    if (gameRef.current) return

    gameRef.current = new Phaser.Game({
      type: Phaser.AUTO,
      parent: containerId,
      width: 800,
      height: 450,
      backgroundColor: '#2f855a',
      scene: MainScene,
    })

    return () => {
      gameRef.current?.destroy(true)
      gameRef.current = null
    }
  }, [containerId])
}
