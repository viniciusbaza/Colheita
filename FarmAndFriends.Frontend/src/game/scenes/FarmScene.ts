import Phaser from 'phaser'
import { gridToIso } from '../iso/isoUtils'
import type { Farm, Plot } from '../../types/Farm'

const TILE_WIDTH = 64
const TILE_HEIGHT = 32
const ISO_OFFSET_Y = 70
const GROWTH_STAGE_2_THRESHOLD = 0.5

const SPROUT_TEXTURES: Record<string, string> = {
  corn: 'sprout-corn',
  carrot: 'sprout-carrot',
}

const READY_TEXTURES: Record<string, string> = {
  corn: 'ready-corn',
  carrot: 'ready-carrot',
}

export default class FarmScene extends Phaser.Scene {
  private farm: Farm

  constructor(farm: Farm) {
    super('FarmScene')
    this.farm = farm
  }

  preload() {
    this.load.image('plot', '/src/game/assets/tiles/plot.png')
    this.load.image('plot-locked', '/src/game/assets/tiles/plot-locked.png')
    this.load.image('plot-glow', '/src/game/assets/tiles/plot-glow.png')
    this.load.image('plot-growing', '/src/game/assets/tiles/plot-growing.png')
    this.load.image('ready-carrot', '/src/game/assets/tiles/ready/carrot.png')
    this.load.image('ready-corn', '/src/game/assets/tiles/ready/corn.png') 
    this.load.image('sprout-carrot', '/src/game/assets/tiles/sprout/carrot.png')
    this.load.image('sprout-corn', '/src/game/assets/tiles/sprout/corn.png')
    
  }

  create() {
    const originX = this.cameras.main.width / 2
    const originY = this.cameras.main.height / 2 

    const isMobile = window.innerWidth < 768
    this.cameras.main.setZoom(isMobile ? 1.4 : 1.8)

    for (const plot of this.farm.plots) {
      this.createPlot(plot, originX, originY)
    }
    // Escuta evento de colheita
    window.addEventListener('plot:harvest:done', this.onHarvestDone)

    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
      window.removeEventListener('plot:harvest:done', this.onHarvestDone)
    })

    // Escuta evento de sincronia da fazenda
    window.addEventListener('farm:sync', this.onFarmSync)

    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
      window.removeEventListener('farm:sync', this.onFarmSync)
    })
  }

  private plotTiles = new Map<string, Phaser.GameObjects.Image>()
  
  private createPlot(plot: Plot, originX: number, originY: number) {
    const { isoX, isoY } = gridToIso(
      plot.x,
      plot.y,
      TILE_WIDTH,
      TILE_HEIGHT
    )

    const texture = this.getPlotTexture(plot)

    const tile = this.add
      .image(originX + isoX, originY + isoY, texture)
      .setOrigin(0.5, 1)
      .setScale(0.11)
      .setDepth(plot.x + plot.y)
      .setInteractive()

    tile.setData('plotId', plot.id)
    this.plotTiles.set(plot.id, tile)

    // 🌟 Efeitos para plots ready
    if (plot.seedId && plot.isReady) {
      this.addReadyPulse(tile)
      this.addGlowEffect(tile)
    }

    // Clique no plot
    tile.on('pointerdown', () => {
      console.log('Plot clicado:', plot.id)

      const cam = this.cameras.main

      const screenX =  (tile.x - cam.worldView.x) * cam.zoom
      const screenY = (tile.y - cam.worldView.y) * cam.zoom
      
      window.dispatchEvent(
        new CustomEvent('plot:click', {
          detail: { 
            plotId: plot.id,
            x: screenX,
            y: screenY - ISO_OFFSET_Y
          }
        })
      )
    })
  }

  private addGlowEffect(tile: Phaser.GameObjects.Image) {
    const glow = this.add.image(tile.x, tile.y, 'plot-glow')
      .setOrigin(0.5, 1)
      .setScale(tile.scale)
      .setAlpha(1)
      .setDepth(tile.depth - 1)

    this.tweens.add({
      targets: glow,
      alpha: { from: 0.5, to: 1 },
      duration: 400,
      ease: 'Sine.easeInOut',
      yoyo: true,
      repeat: -1,
    })

    tile.setData('glow', glow)
  }

  private addReadyPulse(tile: Phaser.GameObjects.Image) {
    this.tweens.add({
      targets: tile,
      scale: tile.scale * 1.03,
      duration: 900,
      ease: 'Sine.easeInOut',
      yoyo: true,
      repeat: -1,
    })
  }

  private getPlotTexture(plot: Plot) {

    if (!plot.unlocked) return 'plot-locked'
    
    if (plot.seedId && plot.isReady) {
    return READY_TEXTURES[plot.seedId] ?? 'plot-growing'
    }

    if (plot.seedId) {
      const progress = getGrowthProgress(plot)

      if (progress >= GROWTH_STAGE_2_THRESHOLD) {
      return SPROUT_TEXTURES[plot.seedId] ?? 'plot-growing'
      }

      return 'plot-growing'
    }
    return 'plot'
  }

  private onFarmSync = (e: Event) => {
    const farm = (e as CustomEvent<Farm>).detail

    this.farm = farm

    // Atualiza cada plot
    for (const plot of farm.plots) {
      const tile = this.plotTiles.get(plot.id)
      if (!tile) continue

      const newTexture = this.getPlotTexture(plot)

      if (tile.texture.key !== newTexture) {
        tile.setTexture(newTexture)
      }

      // 🌟 ready → adiciona efeitos
      if (plot.seedId && plot.isReady) {
        if (!tile.getData('glow')) {
          this.addReadyPulse(tile)
          this.addGlowEffect(tile)
        }
      } else {
        // 🧹 remove efeitos se não estiver ready
        const glow = tile.getData('glow')
        glow?.destroy()
        tile.setData('glow', null)
        this.tweens.killTweensOf(tile)
      }
    }
  }

  private onHarvestDone = async (e: Event) => {
    const { plotId, xpGained } = (e as CustomEvent<{
      plotId: string
      xpGained: number
    }>).detail

    const tile = this.plotTiles.get(plotId)
    if (!tile) return

    this.harvestPlot(tile, xpGained)
  }

  private harvestPlot(
    tile: Phaser.GameObjects.Image,
    xpGained: number
  ) {
    tile.setAlpha(0.3)
    tile.disableInteractive()

    // 🔥 Remove glow e pulse
    const glow = tile.getData('glow')
    glow?.destroy()
    tile.setData('glow', null)
    this.tweens.killTweensOf(tile)

    // 🌱 Volta ao estado de plot vazio
    tile.setScale(0.11)

    // 🌾 Animação de colheita
    this.tweens.add({
      targets: tile,
      scale: tile.scale * 1.2,
      duration: 150,
      yoyo: true,
      repeat: 1,
      onComplete: () => {
        tile.setAlpha(1)
      }
    })

    // ✨ XP FLOAT (valor REAL vindo do backend)
    this.spawnXp(tile.x, tile.y - 30, xpGained)

    // 🔄 Atualiza textura para plot vazio após animação
    tile.setTexture('plot')
    tile.setAlpha(1)
  }

  private spawnXp(x: number, y: number, amount: number) {
    const text = this.add.text(x, y, `+${amount} XP`, {
      fontSize: '16px',
      color: '#7CFF7C',
      stroke: '#1B5E20',
      strokeThickness: 3,
    })
      .setOrigin(0.5)
      .setDepth(999)

    this.tweens.add({
      targets: text,
      y: y - 30,
      alpha: 0,
      scale: 1.2,
      duration: 800,
      ease: 'Cubic.easeOut',
      onComplete: () => text.destroy(),
    })
  }
}

function getGrowthProgress(plot: Plot): number {
  if (!plot.plantedAt || !plot.readyAt) return 0

  const planted = new Date(plot.plantedAt).getTime()
  const ready = new Date(plot.readyAt).getTime()
  const now = Date.now()

  const total = ready - planted
  const elapsed = now - planted

  return Math.min(Math.max(elapsed / total, 0), 1)
}