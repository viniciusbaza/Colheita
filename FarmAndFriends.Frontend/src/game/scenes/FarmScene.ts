import Phaser from 'phaser'
import { gridToIso } from '../iso/isoUtils'
import type {
  Farm,
  Plot,
  PlotHarvestDone,
  PlotPlantDone,
  PlotStealDone,
} from '../../types/Farm'

type XpSource = 'HARVEST' | 'PLANT' | 'STEAL'

const TILE_WIDTH = 64
const TILE_HEIGHT = 32
const ISO_OFFSET_Y = 70
const GROWTH_STAGE_2_THRESHOLD = 0.5

const SPROUT_TEXTURES: Record<string, string> = {
  corn: 'sprout-corn',
  carrot: 'sprout-carrot',
  tomato: 'sprout-tomato'
}

const READY_TEXTURES: Record<string, string> = {
  corn: 'ready-corn',
  carrot: 'ready-carrot',
  tomato: 'ready-tomato'
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

export default class FarmScene extends Phaser.Scene {
  private farm!: Farm
  private modalBlockers = new Set<string>()

  constructor() {
    super('FarmScene')
  }

  init(data: { farm: Farm}) {
    this.farm = data.farm
  }

  preload() {
    const file = 'thick_8x8';
    this.load.bitmapFont(
      'farm-font',
      '/src/game/assets/fonts/' + file + '.png',
      '/src/game/assets/fonts/' + file + '.xml'
    )

    this.load.image('plot', '/src/game/assets/tiles/plot.png')
    this.load.image('plot-locked', '/src/game/assets/tiles/plot-locked.png')
    this.load.image('plot-glow', '/src/game/assets/tiles/plot-glow.png')
    this.load.image('plot-growing', '/src/game/assets/tiles/plot-growing.png')
    this.load.image('sprout-carrot', '/src/game/assets/tiles/sprout/carrot.png')
    this.load.image('ready-carrot', '/src/game/assets/tiles/ready/carrot.png')
    this.load.image('sprout-corn', '/src/game/assets/tiles/sprout/corn.png')
    this.load.image('ready-corn', '/src/game/assets/tiles/ready/corn.png') 
    this.load.image('sprout-tomato', '/src/game/assets/tiles/sprout/tomato.png')
    this.load.image('ready-tomato', '/src/game/assets/tiles/ready/tomato.png') 
  }

  create() {
    const originX = this.cameras.main.width / 2
    const originY = this.cameras.main.height / 2 

    const isMobile = window.innerWidth < 768
    this.cameras.main.setZoom(isMobile ? 1.4 : 1.8)
    this.cameras.main.fadeIn(200)
    this.children.removeAll()
    this.tweens.killAll()
    this.plotTiles.clear()

    for (const plot of this.farm.plots) {
      this.createPlot(plot, originX, originY)
    }
    // Escuta evento de plantio
    window.addEventListener('plot:plant:done', this.onPlantDone)

    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
      window.removeEventListener('plot:plant:done', this.onPlantDone)
    })

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

    // Evento para bloquear o input
    window.addEventListener('ui:modal', this.onModalToggle)

    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
      window.removeEventListener('ui:modal', this.onModalToggle)
      this.modalBlockers.clear()
    })

    // Evento de roubo
    window.addEventListener('plot:steal:done', this.onStealDone)

    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
      window.removeEventListener('plot:steal:done', this.onStealDone)
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

    const ground = this.add.zone(
      originX + isoX,
      originY + isoY,
      TILE_WIDTH,
      TILE_HEIGHT
    )

    ground
      .setOrigin(0.5, 1)
      .setDepth(plot.x + plot.y)
      .setInteractive(
        new Phaser.Geom.Polygon([
          TILE_WIDTH / 2, 0,
          TILE_WIDTH, TILE_HEIGHT / 2,
          TILE_WIDTH / 2, TILE_HEIGHT,
          0, TILE_HEIGHT / 2
        ]),
        Phaser.Geom.Polygon.Contains
      )

    // 🔍 debug visual do hit box
    //this.input.enableDebug(ground)

    tile.setData('plotId', plot.id)

    this.plotTiles.set(plot.id, tile)

    // 🌟 Efeitos para plots ready
    if (plot.seedId && plot.isReady) {
      this.addReadyPulse(tile)
      this.addGlowEffect(tile)
      if (plot.remainingYield > 0) {
        this.addRemainingYieldBadge(tile, plot.remainingYield)
      }
    }

    // Clique no plot
    ground.on('pointerdown', () => {
      console.log('Plot clicado:', plot.id)

      const cam = this.cameras.main

      const screenX = (tile.x - cam.worldView.x) * cam.zoom
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

  private addRemainingYieldBadge(tile: Phaser.GameObjects.Image, remainingYield: number) {
    const badge = this.add.container(tile.x + 5, tile.y - 5)

    // sombra
    const shadow = this.add.graphics()
    shadow.fillStyle(0x000000, 0.25)
    shadow.fillRoundedRect(-15, -8, 20, 12, 6)
    shadow.y = 1

    // fundo
    const bg = this.add.graphics()
    bg.fillStyle(0x4CAF50, 1)
    bg.fillRoundedRect(-15, -10, 20, 12, 6)

    // texto
    const text = this.add.bitmapText(-5, -2,
      'farm-font', `x${remainingYield}`, 7
    ).setOrigin(0.5)

    badge.add([shadow, bg, text])

    badge
      .setDepth(tile.depth + 10)
      .setScale(0)

    // animação de entrada
    this.tweens.add({
      targets: badge,
      scale: 1,
      duration: 220,
      ease: 'Back.out'
    })

    tile.setData('yieldBadge', {
      container: badge,
      text
    })
  }

  private removeRemainingYieldBadge(tile: Phaser.GameObjects.Image) {
    const data = tile.getData('yieldBadge') as {
      container: Phaser.GameObjects.Container,
      text: Phaser.GameObjects.BitmapText
    }

    if (!data) return

    const { container, text } = data
    if (!text || !container) return

    this.tweens.add({
      targets: container,
      scale: 0,
      duration: 150,
      ease: 'Back.in',
      onComplete: () => container.destroy()
    })

    tile.setData('yieldBadge', null)
  }

  private updateRemainingYieldBadge(tile: Phaser.GameObjects.Image, remainingYield: number ) {
    const data = tile.getData('yieldBadge') as {
      container: Phaser.GameObjects.Container
      text: Phaser.GameObjects.BitmapText
    }

    if (!data) return

    const { container, text } = data
    if (!text || !container) return

    text.setText(`x${remainingYield}`)

    this.tweens.add({
      targets: container,
      scale: 1.15,
      duration: 120,
      yoyo: true,
      ease: 'Sine.easeInOut'
    })

    // 🔴 feedback de roubo
    text.setTint(0xff5555)

    this.time.delayedCall(400, () => {
      if (!text.scene) return
      text.clearTint()
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

  private onModalToggle = (e: Event) => {
    const { source = 'legacy', open } = (
      e as CustomEvent<{ source?: string; open: boolean }>
    ).detail

    if (open) {
      this.modalBlockers.add(source)
    } else {
      this.modalBlockers.delete(source)
    }

    this.input.enabled = this.modalBlockers.size === 0
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
        const badge = tile.getData('yieldBadge')
        if (!badge && plot.remainingYield > 0) {
          this.addRemainingYieldBadge(tile, plot.remainingYield)
        }
      } else {
        // 🧹 remove efeitos se não estiver ready
        const glow = tile.getData('glow')
        glow?.destroy()
        tile.setData('glow', null)
        this.tweens.killTweensOf(tile)
        this.removeRemainingYieldBadge(tile)
      }
    }
  }

  private onHarvestDone = async (e: Event) => {
    const { plotId, xpGained } = (e as CustomEvent<PlotHarvestDone>).detail

    const tile = this.plotTiles.get(plotId)
    if (!tile) return

    this.harvestPlot(tile, xpGained)
  }

  private onPlantDone = (e: Event) => {
    const { plotId, xpGained } = (e as CustomEvent<PlotPlantDone>).detail

    const tile = this.plotTiles.get(plotId)
    if (!tile) return

    this.time.delayedCall(0, () => {
      this.spawnXp(tile.x, tile.y - 30, xpGained, 'PLANT')
    })
  }

  private harvestPlot(tile: Phaser.GameObjects.Image, xpGained: number) {
    tile.setAlpha(0.3)
    tile.disableInteractive()

    // 🔥 Remove glow e pulse
    const glow = tile.getData('glow')
    glow?.destroy()
    tile.setData('glow', null)
    this.tweens.killTweensOf(tile)
    this.removeRemainingYieldBadge(tile)

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
    this.spawnXp(tile.x, tile.y - 30, xpGained, 'HARVEST')

    // 🔄 Atualiza textura para plot vazio após animação
    tile.setTexture('plot')
    tile.setAlpha(1)
  }

  private onStealDone = async (e: Event) => {
    const { plotId, remainingYield, xpGained } = (e as CustomEvent<PlotStealDone>).detail

    const tile = this.plotTiles.get(plotId)
    if (!tile) return

    // ⏱ aguarda o Phaser estabilizar a cena
    this.time.delayedCall(0, () => {
      // 1️⃣ Atualiza badge
      this.updateRemainingYieldBadge(tile, remainingYield)
      // 2️⃣ Animação de roubo
      this.animatePlotSteal(tile)
      // 3 XP Float
      this.spawnXp(tile.x, tile.y - 30, xpGained, 'STEAL')
    })
  }

  private animatePlotSteal(tile: Phaser.GameObjects.Image) {

    this.tweens.add({
      targets: tile,
      //angle: { from: -5, to: 5 },
      x: tile.x + 2,
      yoyo: true,
      repeat: 4,
      duration: 60
    })

    // 💨 Partícula / fumaça
    const puff = this.add.circle(
      tile.x,
      tile.y - 12,
      6,
      0x000000,
      0.3
    )

    this.tweens.add({
      targets: puff,
      y: puff.y - 20,
      alpha: 0,
      scale: 1.5,
      duration: 500,
      onComplete: () => puff.destroy()
    })
  }

  private spawnXp(x: number, y: number, amount: number, source: XpSource = 'HARVEST') {
    const styles = {
      HARVEST: {
        color: '#7CFF7C',
        stroke: '#1B5E20',
        scale: 1.2,
      },
      PLANT: {
        color: '#ffd17c',
        stroke: '#5e341b',
        scale: 1.2,
      },
      STEAL: {
        color: '#FF9C6E',
        stroke: '#7A2E1B',
        scale: 1.3,
      },
    }

    const style = styles[source]

    const text = this.add.text(x, y, `+${amount} XP`, {
      fontSize: '16px',
      color: style.color,
      stroke: style.stroke,
      strokeThickness: 3,
    })
      .setOrigin(0.5)
      .setDepth(999)

    this.tweens.add({
      targets: text,
      y: y - 30,
      alpha: 0,
      scale: style.scale,
      duration: 800,
      ease: 'Cubic.easeOut',
      onComplete: () => text.destroy(),
    })
  }
}
