import Phaser from 'phaser'
import { gridToIso } from '../iso/isoUtils'
import farmFontImage from '../assets/fonts/thick_8x8.png'
import farmFontData from '../assets/fonts/thick_8x8.xml?url'
import farmEnvironment from '../assets/environment/farm-clearing.png'
import plotImage from '../assets/tiles/plot.png'
import plotLockedImage from '../assets/tiles/plot-locked.png'
import plotGlowImage from '../assets/tiles/plot-glow.png'
import plotGrowingImage from '../assets/tiles/plot-growing.png'
import {
  calculateFarmEnvironmentExpansion,
  calculateFarmPlotLayout,
  FARM_TILE_HEIGHT,
  FARM_TILE_WIDTH,
} from '../farmLayout'
import {
  FARM_CAMERA_MODE_EVENT,
  type FarmCameraMode,
  type FarmCameraModeDetail,
} from '../farmCamera'
import sproutCarrotImage from '../assets/tiles/sprout/carrot.png'
import sproutCornImage from '../assets/tiles/sprout/corn.png'
import sproutPumpkinImage from '../assets/tiles/sprout/pumpkin.png'
import sproutTomatoImage from '../assets/tiles/sprout/tomato.png'
import readyCarrotImage from '../assets/tiles/ready/carrot.png'
import readyCornImage from '../assets/tiles/ready/corn.png'
import readyPumpkinImage from '../assets/tiles/ready/pumpkin.png'
import readyTomatoImage from '../assets/tiles/ready/tomato.png'
import type {
  Farm,
  Plot,
  PlotHarvestDone,
  PlotPlantDone,
  PlotStealDone,
} from '../../types/Farm'

type XpSource = 'HARVEST' | 'PLANT' | 'STEAL'

const GROWTH_STAGE_2_THRESHOLD = 0.5
const PLOT_SCALE = 0.11
const ENVIRONMENT_SCALE = 0.42
const CAMERA_DRAG_THRESHOLD = 8

const DEPTH = {
  BACKDROP: -1_000,
  CLOUDS: -900,
  ENVIRONMENT: -300,
  PLOTS: 100,
  BADGES: 500,
  FEEDBACK: 999,
} as const

const SPROUT_TEXTURES: Record<string, string> = {
  corn: 'sprout-corn',
  carrot: 'sprout-carrot',
  pumpkin: 'sprout-pumpkin',
  tomato: 'sprout-tomato',
}

const READY_TEXTURES: Record<string, string> = {
  corn: 'ready-corn',
  carrot: 'ready-carrot',
  pumpkin: 'ready-pumpkin',
  tomato: 'ready-tomato',
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
  private environment?: Phaser.GameObjects.Image
  private modalBlockers = new Set<string>()
  private plotTiles = new Map<string, Phaser.GameObjects.Image>()
  private plotBounds = new Phaser.Geom.Rectangle()
  private cameraContentBounds = new Phaser.Geom.Rectangle()
  private cameraMode: FarmCameraMode = 'focus'
  private overviewZoom = 1
  private focusZoom = 1
  private hudSafeArea = 96
  private cameraTween: Phaser.Tweens.Tween | null = null
  private panPointerId: number | null = null
  private panStartX = 0
  private panStartY = 0
  private panStartCenterX = 0
  private panStartCenterY = 0
  private panMoved = false
  private suppressPlotClickUntil = 0
  private readonly reduceMotion = window.matchMedia(
    '(prefers-reduced-motion: reduce)',
  ).matches

  constructor() {
    super('FarmScene')
  }

  init(data: { farm: Farm}) {
    this.farm = data.farm
  }

  preload() {
    this.load.bitmapFont('farm-font', farmFontImage, farmFontData)
    this.load.image('farm-environment', farmEnvironment)
    this.load.image('plot', plotImage)
    this.load.image('plot-locked', plotLockedImage)
    this.load.image('plot-glow', plotGlowImage)
    this.load.image('plot-growing', plotGrowingImage)
    this.load.image('sprout-carrot', sproutCarrotImage)
    this.load.image('ready-carrot', readyCarrotImage)
    this.load.image('sprout-corn', sproutCornImage)
    this.load.image('ready-corn', readyCornImage)
    this.load.image('sprout-pumpkin', sproutPumpkinImage)
    this.load.image('ready-pumpkin', readyPumpkinImage)
    this.load.image('sprout-tomato', sproutTomatoImage)
    this.load.image('ready-tomato', readyTomatoImage)
  }

  create() {
    this.plotTiles.clear()
    this.modalBlockers.clear()
    this.cameraMode =
      this.registry.get('farmCameraMode') === 'overview'
        ? 'overview'
        : 'focus'
    this.cancelPan()

    const plotLayout = calculateFarmPlotLayout(this.farm.plots)
    this.plotBounds.setTo(
      plotLayout.bounds.left,
      plotLayout.bounds.top,
      plotLayout.bounds.width,
      plotLayout.bounds.height,
    )

    this.cameras.main.setBackgroundColor('#dff5ff')
    this.createBackdrop()
    this.environment = this.add
      .image(0, 0, 'farm-environment')
      .setScale(
        ENVIRONMENT_SCALE *
          calculateFarmEnvironmentExpansion(plotLayout.bounds),
      )
      .setDepth(DEPTH.ENVIRONMENT)

    this.updateCameraContentBounds()

    for (const plot of this.farm.plots) {
      this.createPlot(plot, plotLayout.originX, plotLayout.originY)
    }

    this.layoutCamera(this.scale.gameSize.width, this.scale.gameSize.height)
    this.cameras.main.fadeIn(260)
    this.scale.on(Phaser.Scale.Events.RESIZE, this.onResize)
    window.addEventListener(FARM_CAMERA_MODE_EVENT, this.onCameraModeChange)
    this.input.on('pointerdown', this.onPanPointerDown)
    this.input.on('pointermove', this.onPanPointerMove)
    this.input.on('pointerup', this.onPanPointerUp)
    this.input.on('pointerupoutside', this.onPanPointerUp)
    this.input.on('gameout', this.onPointerLeave)
    this.game.canvas.style.touchAction = 'none'
    this.updateCanvasCursor()

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
      this.scale.off(Phaser.Scale.Events.RESIZE, this.onResize)
      window.removeEventListener(
        FARM_CAMERA_MODE_EVENT,
        this.onCameraModeChange,
      )
      this.input.off('pointerdown', this.onPanPointerDown)
      this.input.off('pointermove', this.onPanPointerMove)
      this.input.off('pointerup', this.onPanPointerUp)
      this.input.off('pointerupoutside', this.onPanPointerUp)
      this.input.off('gameout', this.onPointerLeave)
      this.cancelPan()
      this.stopCameraMotion()
      this.game.canvas.style.cursor = 'default'
    })
  }

  private createBackdrop() {
    const backdrop = this.add.graphics().setDepth(DEPTH.BACKDROP)

    backdrop.fillGradientStyle(
      0xdff5ff,
      0xdff5ff,
      0xeefbdd,
      0xeefbdd,
      1,
    )
    backdrop.fillRect(-2_000, -1_500, 4_000, 3_000)

    backdrop.fillStyle(0xfff4b7, 0.28)
    backdrop.fillCircle(350, -255, 88)
    backdrop.fillStyle(0xffe784, 0.72)
    backdrop.fillCircle(350, -255, 54)

    backdrop.fillStyle(0x82ba58, 0.52)
    backdrop.fillEllipse(-350, 65, 1_350, 430, 96)
    backdrop.fillEllipse(610, 35, 1_300, 390, 96)
    backdrop.fillStyle(0x63a04b, 0.68)
    backdrop.fillEllipse(-610, 175, 1_550, 450, 96)
    backdrop.fillEllipse(520, 185, 1_650, 470, 96)
    backdrop.fillStyle(0x438243, 0.36)
    backdrop.fillEllipse(0, 290, 2_300, 560, 128)

    this.createCloud(-330, -245, 0.9, 0.72, 34)
    this.createCloud(115, -305, 0.62, 0.58, 24)
  }

  private createCloud(
    x: number,
    y: number,
    scale: number,
    alpha: number,
    drift: number,
  ) {
    const cloud = this.add.graphics({ x, y }).setDepth(DEPTH.CLOUDS)
    cloud.fillStyle(0xffffff, alpha)
    cloud.fillEllipse(0, 12, 116, 34)
    cloud.fillCircle(-28, 3, 23)
    cloud.fillCircle(4, -4, 31)
    cloud.fillCircle(35, 5, 22)
    cloud.setScale(scale)

    if (!this.reduceMotion) {
      this.tweens.add({
        targets: cloud,
        x: x + drift,
        duration: 8_500 + Math.abs(x),
        ease: 'Sine.easeInOut',
        yoyo: true,
        repeat: -1,
      })
    }
  }

  private onResize = (gameSize: Phaser.Structs.Size) => {
    const previousCenter =
      this.cameraMode === 'overview'
        ? this.getCameraCenter()
        : undefined

    this.cancelPan()
    this.stopCameraMotion()
    this.cameras.resize(gameSize.width, gameSize.height)
    this.layoutCamera(gameSize.width, gameSize.height, previousCenter)
  }

  private updateCameraContentBounds() {
    const environment = this.environment
    if (!environment) return

    const environmentBounds = environment.getBounds()
    const left = Math.min(environmentBounds.left, this.plotBounds.left)
    const top = Math.min(environmentBounds.top, this.plotBounds.top)
    const right = Math.max(environmentBounds.right, this.plotBounds.right)
    const bottom = Math.max(environmentBounds.bottom, this.plotBounds.bottom)

    this.cameraContentBounds.setTo(
      left,
      top,
      right - left,
      bottom - top,
    )
  }

  private layoutCamera(
    width: number,
    height: number,
    preservedPanCenter?: Phaser.Math.Vector2,
  ) {
    const environment = this.environment
    if (!environment || width <= 0 || height <= 0) return

    const camera = this.cameras.main
    const isPortrait = height >= width * 1.25
    const compactLayout = width < 768 || height < 600
    this.hudSafeArea = compactLayout ? 86 : 96

    if (isPortrait) {
      const fitWidth = (width - 24) / this.plotBounds.width
      const fitHeight =
        (height - this.hudSafeArea - 24) / this.plotBounds.height

      // O 3x3 fica mais próximo no celular. Grades maiores ainda cabem por
      // inteiro na visão geral, sem assumir um número fixo de linhas/colunas.
      this.overviewZoom = Math.max(
        0.35,
        Math.min(fitWidth, fitHeight, 1.6),
      )
    } else {
      const horizontalPadding = compactLayout ? 24 : 64
      const verticalPadding = compactLayout ? 12 : 36
      const fitWidth =
        (width - horizontalPadding) / this.cameraContentBounds.width
      const fitHeight =
        (height - this.hudSafeArea - verticalPadding) /
        this.cameraContentBounds.height

      this.overviewZoom = Math.max(
        0.35,
        Math.min(fitWidth, fitHeight, compactLayout ? 1.35 : 1.9),
      )
    }

    this.focusZoom = Math.min(
      compactLayout ? 2.1 : 2.4,
      Math.max(
        compactLayout ? 1.35 : 1.85,
        this.overviewZoom * 1.3,
      ),
    )

    const zoom =
      this.cameraMode === 'focus'
        ? this.focusZoom
        : this.overviewZoom
    let center = preservedPanCenter ?? this.getHomeCameraCenter(zoom)

    if (this.cameraMode === 'overview') {
      center = this.clampPanCenter(center.x, center.y, zoom)
    }

    camera.setZoom(zoom)
    camera.centerOn(center.x, center.y)
  }

  private getHomeCameraCenter(zoom: number) {
    return new Phaser.Math.Vector2(
      this.plotBounds.centerX,
      this.plotBounds.centerY - this.hudSafeArea / (2 * zoom),
    )
  }

  private getCameraCenter() {
    const camera = this.cameras.main

    return new Phaser.Math.Vector2(
      camera.scrollX + camera.width / (2 * camera.zoom),
      camera.scrollY + camera.height / (2 * camera.zoom),
    )
  }

  private clampPanCenter(x: number, y: number, zoom: number) {
    const camera = this.cameras.main
    const bounds = this.cameraContentBounds
    const margin = this.scale.gameSize.width < 768 ? 24 : 36
    const worldLeft = bounds.left - margin
    const worldRight = bounds.right + margin
    const worldTop = bounds.top - margin
    const worldBottom = bounds.bottom + margin
    const viewWidth = camera.width / zoom
    const usableViewHeight =
      Math.max(1, camera.height - this.hudSafeArea) / zoom

    let centerX: number
    if (worldRight - worldLeft <= viewWidth) {
      centerX = (worldLeft + worldRight) / 2
    } else {
      centerX = Phaser.Math.Clamp(
        x,
        worldLeft + viewWidth / 2,
        worldRight - viewWidth / 2,
      )
    }

    let centerY: number
    if (worldBottom - worldTop <= usableViewHeight) {
      centerY =
        (worldTop + worldBottom) / 2 - this.hudSafeArea / (2 * zoom)
    } else {
      const minCenterY =
        worldTop + (camera.height / 2 - this.hudSafeArea) / zoom
      const maxCenterY = worldBottom - camera.height / (2 * zoom)
      centerY = Phaser.Math.Clamp(y, minCenterY, maxCenterY)
    }

    return new Phaser.Math.Vector2(centerX, centerY)
  }

  private moveCameraToCurrentMode(animate = true) {
    const camera = this.cameras.main
    const targetZoom =
      this.cameraMode === 'focus'
        ? this.focusZoom
        : this.overviewZoom
    let targetCenter = this.getHomeCameraCenter(targetZoom)

    if (this.cameraMode === 'overview') {
      targetCenter = this.clampPanCenter(
        targetCenter.x,
        targetCenter.y,
        targetZoom,
      )
    }

    this.stopCameraMotion()

    if (!animate || this.reduceMotion) {
      camera.setZoom(targetZoom)
      camera.centerOn(targetCenter.x, targetCenter.y)
      return
    }

    const currentCenter = this.getCameraCenter()
    const tweenState = {
      zoom: camera.zoom,
      centerX: currentCenter.x,
      centerY: currentCenter.y,
    }

    this.cameraTween = this.tweens.add({
      targets: tweenState,
      zoom: targetZoom,
      centerX: targetCenter.x,
      centerY: targetCenter.y,
      duration: 260,
      ease: 'Sine.easeInOut',
      onUpdate: () => {
        camera.setZoom(tweenState.zoom)
        camera.centerOn(tweenState.centerX, tweenState.centerY)
      },
      onComplete: () => {
        camera.setZoom(targetZoom)
        camera.centerOn(targetCenter.x, targetCenter.y)
        this.cameraTween = null
      },
    })
  }

  private stopCameraMotion() {
    this.cameraTween?.stop()
    this.cameraTween = null
  }

  private onCameraModeChange = (event: Event) => {
    const { mode } = (event as CustomEvent<FarmCameraModeDetail>).detail
    if (mode !== 'focus' && mode !== 'overview') return

    this.registry.set('farmCameraMode', mode)

    // Sincronizações da fazenda podem reenviar o modo atual. Não recentraliza
    // nesses casos para preservar o deslocamento feito pelo jogador.
    if (mode === this.cameraMode) return

    this.cameraMode = mode
    this.cancelPan()
    this.moveCameraToCurrentMode()
    this.updateCanvasCursor()
  }

  private onPanPointerDown = (pointer: Phaser.Input.Pointer) => {
    if (
      this.cameraMode !== 'overview' ||
      this.modalBlockers.size > 0 ||
      this.panPointerId !== null
    ) {
      return
    }

    this.stopCameraMotion()
    const center = this.getCameraCenter()
    this.panPointerId = pointer.id
    this.panStartX = pointer.x
    this.panStartY = pointer.y
    this.panStartCenterX = center.x
    this.panStartCenterY = center.y
    this.panMoved = false
    this.updateCanvasCursor(true)
  }

  private onPanPointerMove = (pointer: Phaser.Input.Pointer) => {
    if (pointer.id !== this.panPointerId) return

    const deltaX = pointer.x - this.panStartX
    const deltaY = pointer.y - this.panStartY

    if (
      !this.panMoved &&
      Math.hypot(deltaX, deltaY) < CAMERA_DRAG_THRESHOLD
    ) {
      return
    }

    this.panMoved = true
    const camera = this.cameras.main
    const targetCenter = this.clampPanCenter(
      this.panStartCenterX - deltaX / camera.zoom,
      this.panStartCenterY - deltaY / camera.zoom,
      camera.zoom,
    )

    camera.centerOn(targetCenter.x, targetCenter.y)
  }

  private onPanPointerUp = (pointer: Phaser.Input.Pointer) => {
    if (pointer.id !== this.panPointerId) return

    if (this.panMoved) {
      this.suppressPlotClickUntil = this.time.now + 160
    }

    this.cancelPan()
  }

  private onPointerLeave = () => {
    if (this.panMoved) {
      this.suppressPlotClickUntil = this.time.now + 160
    }

    this.cancelPan()
  }

  private cancelPan() {
    this.panPointerId = null
    this.panMoved = false
    this.updateCanvasCursor()
  }

  private updateCanvasCursor(dragging = false) {
    if (!this.game?.canvas) return

    if (this.cameraMode !== 'overview') {
      this.game.canvas.style.cursor = 'default'
      return
    }

    this.game.canvas.style.cursor = dragging ? 'grabbing' : 'grab'
  }
  
  private createPlot(plot: Plot, originX: number, originY: number) {
    const { isoX, isoY } = gridToIso(
      plot.x,
      plot.y,
      FARM_TILE_WIDTH,
      FARM_TILE_HEIGHT,
    )

    const texture = this.getPlotTexture(plot)

    const tile = this.add
      .image(originX + isoX, originY + isoY, texture)
      .setOrigin(0.5, 1)
      .setScale(PLOT_SCALE)
      .setDepth(DEPTH.PLOTS + plot.x + plot.y)

    const ground = this.add.zone(
      originX + isoX,
      originY + isoY,
      FARM_TILE_WIDTH,
      FARM_TILE_HEIGHT,
    )

    ground
      .setOrigin(0.5, 1)
      .setDepth(DEPTH.PLOTS + plot.x + plot.y)
      .setInteractive(
        new Phaser.Geom.Polygon([
          FARM_TILE_WIDTH / 2, 0,
          FARM_TILE_WIDTH, FARM_TILE_HEIGHT / 2,
          FARM_TILE_WIDTH / 2, FARM_TILE_HEIGHT,
          0, FARM_TILE_HEIGHT / 2,
        ]),
        Phaser.Geom.Polygon.Contains
      )

    // 🔍 debug visual do hit box
    //this.input.enableDebug(ground)

    tile.setData('plotId', plot.id)
    tile.setData('gridX', plot.x)
    tile.setData('gridY', plot.y)

    this.plotTiles.set(plot.id, tile)

    // 🌟 Efeitos para plots ready
    if (plot.seedId && plot.isReady) {
      this.addReadyPulse(tile)
      this.addGlowEffect(tile)
      if (plot.remainingYield > 0) {
        this.addRemainingYieldBadge(tile, plot.remainingYield)
      }
    }

    // Abre somente em tap/click. Um arrasto iniciado sobre o plot move a
    // câmera sem abrir o modal acidentalmente.
    ground.on('pointerup', () => {
      if (
        this.panMoved ||
        this.time.now < this.suppressPlotClickUntil ||
        this.cameraTween
      ) {
        return
      }

      console.log('Plot clicado:', plot.id)

      const cam = this.cameras.main

      const screenX = cam.x + (tile.x - cam.worldView.x) * cam.zoom
      const anchorWorldY = tile.y - tile.displayHeight * 0.78
      const screenY = cam.y + (anchorWorldY - cam.worldView.y) * cam.zoom
      
      window.dispatchEvent(
        new CustomEvent('plot:click', {
          detail: { 
            plotId: plot.id,
            x: screenX,
            y: screenY,
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
      .setDepth(DEPTH.BADGES + tile.depth)
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
      this.cancelPan()
      this.modalBlockers.add(source)
    } else {
      this.modalBlockers.delete(source)
    }

    this.input.enabled = this.modalBlockers.size === 0
  }

  private onFarmSync = (e: Event) => {
    const farm = (e as CustomEvent<Farm>).detail

    this.farm = farm

    const topologyChanged =
      farm.plots.length !== this.plotTiles.size ||
      farm.plots.some(plot => {
        const tile = this.plotTiles.get(plot.id)

        return (
          !tile ||
          tile.getData('gridX') !== plot.x ||
          tile.getData('gridY') !== plot.y
        )
      })

    if (topologyChanged) {
      // Compras futuras podem adicionar plots ou ampliar a matriz. Reiniciar
      // reconstrói tiles, hit areas, bounds e enquadramento de forma atômica.
      this.scene.restart({ farm })
      return
    }

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
    tile.setScale(PLOT_SCALE)

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
      .setDepth(DEPTH.FEEDBACK)

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
