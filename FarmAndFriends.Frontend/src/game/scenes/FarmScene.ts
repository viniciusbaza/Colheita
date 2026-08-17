import Phaser from 'phaser'
import { gridToIso } from '../iso/isoUtils'
import farmFontImage from '../assets/fonts/thick_8x8.png'
import farmFontData from '../assets/fonts/thick_8x8.xml?url'
import farmEnvironment from '../assets/environment/farm-clearing.png'
import plotImage from '../assets/tiles/plot.png'
import plotLockedImage from '../assets/tiles/plot-locked.png'
import plotGlowImage from '../assets/tiles/plot-glow.png'
import plotGrowingImage from '../assets/tiles/plot-growing.png'
import landForSaleSignImage from '../assets/tiles/land-for-sale-sign.png'
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
import { resolveLandOfferPlotId } from '../landOfferVisual'
import { canReceivePlotInput } from '../plotInput'
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
  PestStatus,
  Plot,
  PlotCareDone,
  PlotHarvestDone,
  PlotPestRemoveDone,
  PlotPlantDone,
  PlotStealDone,
} from '../../types/Farm'

type XpSource = 'HARVEST' | 'PLANT' | 'STEAL' | 'CARE' | 'PEST_REMOVE'

const GROWTH_STAGE_2_THRESHOLD = 0.5
const PLOT_SCALE = 0.11
const ENVIRONMENT_SCALE = 0.42
const CAMERA_DRAG_THRESHOLD = 8
const LAND_OFFER_SIGN_WIDTH = FARM_TILE_WIDTH * 0.55
const LAND_OFFER_SIGN_GROUND_OFFSET = FARM_TILE_HEIGHT * 0.1

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

function isPlotProtected(plot: Plot): boolean {
  // FarmMapper only exposes a currently valid protection. Presence is the
  // server decision; the browser clock must not reveal a hidden pest early.
  return plot.protectedUntil !== null
}

export default class FarmScene extends Phaser.Scene {
  private farm!: Farm
  private isVisiting = false
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
  private landOfferSign: Phaser.GameObjects.Image | null = null
  private landOfferPlotId: string | null = null
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

  init(data: { farm: Farm; isVisiting: boolean }) {
    this.farm = data.farm
    this.isVisiting = data.isVisiting
  }

  preload() {
    this.load.bitmapFont('farm-font', farmFontImage, farmFontData)
    this.load.image('farm-environment', farmEnvironment)
    this.load.image('plot', plotImage)
    this.load.image('plot-locked', plotLockedImage)
    this.load.image('plot-glow', plotGlowImage)
    this.load.image('plot-growing', plotGrowingImage)
    this.load.image('land-for-sale-sign', landForSaleSignImage)
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

    this.syncLandOfferSign()

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
    window.addEventListener('plot:care:done', this.onCareDone)
    window.addEventListener('plot:pest:remove:done', this.onPestRemoveDone)
    this.events.once(Phaser.Scenes.Events.DESTROY, this.onSceneDestroy)

    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
      window.removeEventListener('plot:steal:done', this.onStealDone)
      window.removeEventListener('plot:care:done', this.onCareDone)
      window.removeEventListener(
        'plot:pest:remove:done',
        this.onPestRemoveDone,
      )
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
      this.events.off(Phaser.Scenes.Events.DESTROY, this.onSceneDestroy)
      this.removeLandOfferSign()
      this.cleanupPestVisuals()
      this.plotTiles.clear()
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

    tile.setData('plotId', plot.id)
    tile.setData('gridX', plot.x)
    tile.setData('gridY', plot.y)
    tile.setData(
      'receivesPlotInput',
      canReceivePlotInput(plot, this.isVisiting),
    )

    this.plotTiles.set(plot.id, tile)
    tile.setData('lastRemainingYield', plot.remainingYield)
    tile.setData('lastPestStatus', plot.pest?.status ?? null)

    // 🌟 Efeitos para plots ready
    if (plot.seedId && plot.isReady) {
      this.addReadyPulse(tile)
      this.addGlowEffect(tile)
      if (
        typeof plot.remainingYield === 'number'
        && plot.remainingYield > 0
      ) {
        this.addRemainingYieldBadge(tile, plot.remainingYield)
      }
    }

    this.syncCareBadge(tile, plot, false)
    this.syncPestVisual(tile, plot, false)

    if (!canReceivePlotInput(plot, this.isVisiting)) return

    ground.setInteractive(
      new Phaser.Geom.Polygon([
        FARM_TILE_WIDTH / 2, 0,
        FARM_TILE_WIDTH, FARM_TILE_HEIGHT / 2,
        FARM_TILE_WIDTH / 2, FARM_TILE_HEIGHT,
        0, FARM_TILE_HEIGHT / 2,
      ]),
      Phaser.Geom.Polygon.Contains,
    )

    // Abre somente em tap/click. Um arrasto iniciado sobre o plot move a
    // câmera sem abrir o modal acidentalmente. Visitantes não registram este
    // handler nos lotes bloqueados, mas o pan global continua disponível.
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
          },
        }),
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

  private addRemainingYieldBadge(
    tile: Phaser.GameObjects.Image,
    remainingYield: number,
    animate = true,
  ) {
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
      .setScale(animate && !this.reduceMotion ? 0 : 1)

    // animação de entrada
    if (animate && !this.reduceMotion) {
      this.tweens.add({
        targets: badge,
        scale: 1,
        duration: 220,
        ease: 'Back.out'
      })
    }

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

  private setRemainingYieldBadgeValue(
    tile: Phaser.GameObjects.Image,
    remainingYield: number,
  ) {
    const data = tile.getData('yieldBadge') as {
      container: Phaser.GameObjects.Container
      text: Phaser.GameObjects.BitmapText
    }

    if (!data) return

    const { text } = data
    if (!text) return

    text.setText(`x${remainingYield}`)
  }

  private updateRemainingYieldBadge(
    tile: Phaser.GameObjects.Image,
    remainingYield: number,
    feedbackTint = 0xff5555,
  ) {
    const data = tile.getData('yieldBadge') as {
      container: Phaser.GameObjects.Container
      text: Phaser.GameObjects.BitmapText
    }

    if (!data) return

    const { container, text } = data
    if (!text || !container) return

    this.setRemainingYieldBadgeValue(tile, remainingYield)

    if (!this.reduceMotion) {
      this.tweens.add({
        targets: container,
        scale: 1.15,
        duration: 120,
        yoyo: true,
        ease: 'Sine.easeInOut'
      })
    }

    text.setTint(feedbackTint)

    this.time.delayedCall(400, () => {
      if (!text.scene) return
      text.clearTint()
    })
  }

  private syncPestVisual(
    tile: Phaser.GameObjects.Image,
    plot: Plot,
    animate = true,
  ) {
    const pest = plot.pest
    const shouldShow =
      pest?.type === 'caterpillar' &&
      pest.status === 'active' &&
      Boolean(plot.seedId) &&
      plot.isReady &&
      !isPlotProtected(plot)
    const current = tile.getData('pestVisual') as
      | Phaser.GameObjects.Text
      | undefined

    if (!shouldShow) {
      this.removePestVisual(tile)
      return
    }

    if (current?.scene) return

    this.addCaterpillarVisual(tile, animate)
  }

  private addCaterpillarVisual(
    tile: Phaser.GameObjects.Image,
    animate = true,
  ) {
    this.removePestVisual(tile)

    // Emoji glyphs are vendor-rendered. Keeping the conventional left-facing
    // caterpillar on the crop's right side makes its head point to the plant.
    const caterpillar = this.add.text(
      tile.x + tile.displayWidth * 0.25,
      tile.y - tile.displayHeight * 0.62,
      '🐛',
      {
        fontFamily:
          '"Segoe UI Emoji", "Apple Color Emoji", "Noto Color Emoji", sans-serif',
        fontSize: '14px',
        resolution: 3,
        padding: { x: 2, y: 2 },
        shadow: {
          offsetX: 1,
          offsetY: 1,
          color: '#1f2937',
          blur: 0,
          fill: true,
        },
      },
    )
      .setOrigin(0.5)
      .setDepth(DEPTH.BADGES + tile.depth + 2)
      .setScale(animate && !this.reduceMotion ? 0 : 1)

    tile.setData('pestVisual', caterpillar)

    if (animate && !this.reduceMotion) {
      this.tweens.add({
        targets: caterpillar,
        scale: 1,
        duration: 220,
        ease: 'Back.out',
      })
    }

    if (!this.reduceMotion) {
      this.tweens.add({
        targets: caterpillar,
        y: caterpillar.y - 3,
        angle: { from: -3, to: 3 },
        duration: 850,
        ease: 'Sine.easeInOut',
        yoyo: true,
        repeat: -1,
      })
    }
  }

  private removePestVisual(tile: Phaser.GameObjects.Image) {
    const caterpillar = tile.getData('pestVisual') as
      | Phaser.GameObjects.Text
      | undefined

    if (caterpillar) {
      this.tweens.killTweensOf(caterpillar)
      caterpillar.destroy()
    }

    tile.setData('pestVisual', null)
  }

  private spawnPestConsumptionFeedback(
    tile: Phaser.GameObjects.Image,
    consumedAmount: number | null,
  ) {
    const message = consumedAmount && consumedAmount > 0
      ? `🐛 Lagarta comeu ${consumedAmount}`
      : '🐛 Dano de lagarta'
    const y = tile.y - tile.displayHeight * 0.92
    const text = this.add.text(tile.x, y, message, {
      fontFamily: 'Arial, sans-serif',
      fontSize: '12px',
      fontStyle: 'bold',
      color: '#fef3c7',
      stroke: '#78350f',
      strokeThickness: 3,
    })
      .setOrigin(0.5)
      .setDepth(DEPTH.FEEDBACK)

    this.tweens.add({
      targets: text,
      y: y - (this.reduceMotion ? 12 : 26),
      alpha: 0,
      duration: this.reduceMotion ? 650 : 1_100,
      ease: 'Cubic.easeOut',
      onComplete: () => text.destroy(),
    })
  }

  private spawnPestTheftFeedback(tile: Phaser.GameObjects.Image) {
    const y = tile.y - tile.displayHeight - 10
    const text = this.add.text(tile.x, y, '🐛 Lagarta espantada!', {
      fontFamily: 'Arial, sans-serif',
      fontSize: '12px',
      fontStyle: 'bold',
      color: '#ecfccb',
      stroke: '#365314',
      strokeThickness: 3,
    })
      .setOrigin(0.5)
      .setDepth(DEPTH.FEEDBACK)

    this.tweens.add({
      targets: text,
      y: y - (this.reduceMotion ? 10 : 22),
      alpha: 0,
      duration: this.reduceMotion ? 600 : 950,
      ease: 'Cubic.easeOut',
      onComplete: () => text.destroy(),
    })
  }

  private cleanupPestVisuals() {
    for (const tile of this.plotTiles.values()) {
      this.removePestVisual(tile)
    }
  }

  private syncCareBadge(
    tile: Phaser.GameObjects.Image,
    plot: Plot,
    animate = true,
  ) {
    const nextStatus = plot.care?.canCare && plot.care?.rewardAvailable === true ? 'available' : null
    const currentStatus = tile.getData('careBadgeStatus') as
      | 'available'
      | null

    if (currentStatus === nextStatus) return

    this.removeCareBadge(tile)

    if (nextStatus === 'available') {
      this.addAvailableCareBadge(tile, animate)
    }
  }

  private addAvailableCareBadge(
    tile: Phaser.GameObjects.Image,
    animate = true,
  ) {
    const badge = this.add.container(
      tile.x - 17,
      tile.y - tile.displayHeight * 0.72,
    )
    const shadow = this.add.circle(1, 2, 7, 0x082f49, 0.2)
    const background = this.add.circle(0, 0, 6.5, 0xe0f2fe, 0.96)
      .setStrokeStyle(1, 0x38bdf8, 1)
    const drop = this.add.graphics()
    drop.fillStyle(0x0ea5e9, 1)
    drop.fillTriangle(0, -4.5, -3, 1, 3, 1)
    drop.fillCircle(0, 1.5, 3)
    drop.fillStyle(0xffffff, 0.65)
    drop.fillCircle(-1.1, 0, 0.8)

    badge
      .add([shadow, background, drop])
      .setDepth(DEPTH.BADGES + tile.depth)
      .setScale(animate && !this.reduceMotion ? 0 : 1)

    tile.setData('careBadge', badge)
    tile.setData('careBadgeStatus', 'available')

    if (animate && !this.reduceMotion) {
      this.tweens.add({
        targets: badge,
        scale: 1,
        duration: 220,
        ease: 'Back.out',
      })
    }

    if (!this.reduceMotion) {
      this.tweens.add({
        targets: badge,
        y: badge.y - 3,
        duration: 1_100,
        ease: 'Sine.easeInOut',
        yoyo: true,
        repeat: -1,
      })
    }
  }

  private removeCareBadge(tile: Phaser.GameObjects.Image) {
    const badge = tile.getData('careBadge') as
      | Phaser.GameObjects.Container
      | undefined

    if (badge) {
      this.tweens.killTweensOf(badge)
      badge.destroy(true)
    }

    tile.setData('careBadge', null)
    tile.setData('careBadgeStatus', null)
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

  private syncLandOfferSign() {
    if (this.isVisiting) {
      this.removeLandOfferSign()
      return
    }

    const plotId = resolveLandOfferPlotId(this.farm)

    if (!plotId) {
      this.removeLandOfferSign()
      return
    }

    const tile = this.plotTiles.get(plotId)

    if (!tile) {
      this.removeLandOfferSign()
      return
    }

    if (
      this.landOfferSign?.active &&
      this.landOfferPlotId === plotId
    ) {
      return
    }

    this.removeLandOfferSign()

    const sign = this.add
      .image(
        tile.x,
        tile.y - LAND_OFFER_SIGN_GROUND_OFFSET,
        'land-for-sale-sign',
      )
      .setName('land-offer-sign')
      .setOrigin(0.5, 1)
      .setDepth(tile.depth + 0.5)

    sign.setScale(LAND_OFFER_SIGN_WIDTH / sign.width)

    sign.setData('plotId', plotId)
    this.landOfferSign = sign
    this.landOfferPlotId = plotId
  }

  private removeLandOfferSign() {
    const sign = this.landOfferSign

    if (sign) {
      this.tweens.killTweensOf(sign)
      sign.destroy()
    }

    this.landOfferSign = null
    this.landOfferPlotId = null
  }

  private onSceneDestroy = () => {
    this.removeLandOfferSign()
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
          tile.getData('gridY') !== plot.y ||
          tile.getData('receivesPlotInput') !== canReceivePlotInput(
            plot,
            this.isVisiting,
          )
        )
      })

    if (topologyChanged) {
      // Compras futuras podem adicionar plots ou ampliar a matriz. Reiniciar
      // reconstrói tiles, hit areas, bounds e enquadramento de forma atômica.
      this.scene.restart({ farm, isVisiting: this.isVisiting })
      return
    }

    this.syncLandOfferSign()

    // Atualiza cada plot
    for (const plot of farm.plots) {
      const tile = this.plotTiles.get(plot.id)
      if (!tile) continue

      const pest = plot.pest
      const previousRemainingYield = tile.getData('lastRemainingYield') as
        | number
        | null
        | undefined
      const previousPestStatus = tile.getData('lastPestStatus') as
        | PestStatus
        | null
        | undefined
      const pestConsumedWithReduction =
        previousPestStatus !== 'consumed' &&
        pest?.status === 'consumed' &&
        typeof previousRemainingYield === 'number' &&
        typeof plot.remainingYield === 'number' &&
        plot.remainingYield < previousRemainingYield
      const newTexture = this.getPlotTexture(plot)

      if (tile.texture.key !== newTexture) {
        tile.setTexture(newTexture)
      }

      this.syncCareBadge(tile, plot)

      // 🌟 ready → adiciona efeitos
      if (plot.seedId && plot.isReady) {
        if (!tile.getData('glow')) {
          this.addReadyPulse(tile)
          this.addGlowEffect(tile)
        }

        if (
          typeof plot.remainingYield === 'number'
          && plot.remainingYield > 0
        ) {
          const badge = tile.getData('yieldBadge')
          if (!badge) {
            this.addRemainingYieldBadge(
              tile,
              plot.remainingYield,
              !pestConsumedWithReduction,
            )
          }

          if (previousRemainingYield !== plot.remainingYield) {
            if (pestConsumedWithReduction) {
              this.updateRemainingYieldBadge(
                tile,
                plot.remainingYield,
                0xf59e0b,
              )
            } else {
              this.setRemainingYieldBadgeValue(
                tile,
                plot.remainingYield,
              )
            }
          }
        } else {
          this.removeRemainingYieldBadge(tile)
        }
      } else {
        // 🧹 remove efeitos se não estiver ready
        const glow = tile.getData('glow')
        this.tweens.killTweensOf(glow)
        glow?.destroy()
        tile.setData('glow', null)
        this.tweens.killTweensOf(tile)
        this.removeRemainingYieldBadge(tile)
      }

      this.syncPestVisual(tile, plot)

      if (pestConsumedWithReduction) {
        this.spawnPestConsumptionFeedback(
          tile,
          pest?.consumedAmount ?? null,
        )
      }

      tile.setData('lastRemainingYield', plot.remainingYield)
      tile.setData('lastPestStatus', pest?.status ?? null)
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
    this.tweens.killTweensOf(glow)
    glow?.destroy()
    tile.setData('glow', null)
    this.tweens.killTweensOf(tile)
    this.removeRemainingYieldBadge(tile)
    this.removeCareBadge(tile)
    this.removePestVisual(tile)

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

  private onCareDone = (e: Event) => {
    const {
      plotId,
      coinsGained,
      xpGained,
    } = (e as CustomEvent<PlotCareDone>).detail

    const tile = this.plotTiles.get(plotId)
    if (!tile) return

    this.time.delayedCall(0, () => {
      this.removeCareBadge(tile)
      this.animatePlotCare(tile)

      const rewardX = tile.x
      const rewardY = tile.y - tile.displayHeight * 0.9
      const xpDelay = coinsGained > 0
        ? this.spawnCoinReward(rewardX, rewardY, coinsGained)
        : 0

      if (xpGained > 0) {
        this.time.delayedCall(xpDelay, () => {
          this.spawnXp(rewardX, rewardY, xpGained, 'CARE')
        })
      }
    })
  }

  private onPestRemoveDone = (e: Event) => {
    const {
      plotId,
      pestOccurrenceId,
      coinsGained,
      xpGained,
    } = (e as CustomEvent<PlotPestRemoveDone>).detail

    const tile = this.plotTiles.get(plotId)
    if (!tile) return
    const currentPlot = this.farm.plots.find(plot => plot.id === plotId)

    // An idempotent retry can arrive after another planting cycle. Keep a
    // confirmed result from hiding or rewarding feedback over a newer pest.
    if (currentPlot?.pest?.occurrenceId !== pestOccurrenceId) return

    this.removePestVisual(tile)

    const rewardX = tile.x
    const rewardY = tile.y - tile.displayHeight * 0.9
    const xpDelay = coinsGained > 0
      ? this.spawnCoinReward(rewardX, rewardY, coinsGained)
      : 0

    if (xpGained > 0) {
      this.time.delayedCall(xpDelay, () => {
        this.spawnXp(rewardX, rewardY, xpGained, 'PEST_REMOVE')
      })
    } else if (coinsGained <= 0) {
      this.spawnPestRemovalFeedback(rewardX, rewardY)
    }
  }

  private animatePlotCare(tile: Phaser.GameObjects.Image) {
    tile.setTint(0x93c5fd)

    this.time.delayedCall(this.reduceMotion ? 120 : 480, () => {
      if (tile.scene) tile.clearTint()
    })

    if (this.reduceMotion) return

    for (let index = 0; index < 4; index += 1) {
      const drop = this.add.circle(
        tile.x - 12 + index * 8,
        tile.y - tile.displayHeight - 16 - (index % 2) * 6,
        2.5,
        0x38bdf8,
        0.9,
      ).setDepth(DEPTH.FEEDBACK)

      this.tweens.add({
        targets: drop,
        y: tile.y - tile.displayHeight * 0.25,
        alpha: 0,
        scale: 0.6,
        delay: index * 55,
        duration: 360,
        ease: 'Quad.easeIn',
        onComplete: () => drop.destroy(),
      })
    }

    this.tweens.add({
      targets: tile,
      scale: tile.scale * 1.025,
      duration: 180,
      ease: 'Sine.easeInOut',
      yoyo: true,
    })
  }

  private spawnCoinReward(
    x: number,
    y: number,
    coinsGained: number,
  ) {
    const duration = this.reduceMotion ? 500 : 900
    const text = this.add.text(
      x,
      y,
      `+${coinsGained} 🪙`,
      {
        fontFamily: 'Arial, sans-serif',
        fontSize: '14px',
        fontStyle: 'bold',
        color: '#e0f2fe',
        stroke: '#075985',
        strokeThickness: 3,
      },
    )
      .setOrigin(0.5)
      .setDepth(DEPTH.FEEDBACK)

    this.tweens.add({
      targets: text,
      y: y - 28,
      alpha: 0,
      scale: 1.1,
      duration,
      ease: 'Cubic.easeOut',
      onComplete: () => text.destroy(),
    })

    return duration
  }

  private spawnPestRemovalFeedback(x: number, y: number) {
    const text = this.add.text(
      x,
      y,
      '🐛 Colheita salva\nSem recompensa desta vez',
      {
        fontFamily:
          '"Arial Rounded MT Bold", "Trebuchet MS", Arial, sans-serif',
        fontSize: '12px',
        fontStyle: 'bold',
        color: '#ecfccb',
        stroke: '#365314',
        strokeThickness: 3,
        align: 'center',
      },
    )
      .setOrigin(0.5)
      .setDepth(DEPTH.FEEDBACK)

    this.tweens.add({
      targets: text,
      y: y - (this.reduceMotion ? 8 : 20),
      alpha: 0,
      duration: this.reduceMotion ? 500 : 850,
      ease: 'Cubic.easeOut',
      onComplete: () => text.destroy(),
    })
  }

  private onStealDone = async (e: Event) => {
    const {
      plotId,
      remainingYield,
      xpGained,
      pestCancelled,
    } = (e as CustomEvent<PlotStealDone>).detail

    const tile = this.plotTiles.get(plotId)
    if (!tile) return

    // ⏱ aguarda o Phaser estabilizar a cena
    this.time.delayedCall(0, () => {
      if (pestCancelled) {
        // A confirmação vem no resultado autoritativo do roubo; farm:sync
        // continua sendo a fonte do status persistido da infestação.
        this.removePestVisual(tile)
        this.spawnPestTheftFeedback(tile)
      }
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
    const styles: Record<XpSource, {
      color: string
      stroke: string
      scale: number
      fontFamily?: string
      fontStyle?: string
    }> = {
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
      CARE: {
        color: '#E0F2FE',
        stroke: '#075985',
        scale: 1.2,
      },
      PEST_REMOVE: {
        color: '#D9F99D',
        stroke: '#365314',
        scale: 1.2,
        fontFamily:
          '"Arial Rounded MT Bold", "Trebuchet MS", Arial, sans-serif',
        fontStyle: 'bold',
      },
    }

    const style = styles[source]
    const textStyle: Phaser.Types.GameObjects.Text.TextStyle = {
      fontSize: '16px',
      color: style.color,
      stroke: style.stroke,
      strokeThickness: 3,
    }

    if (style.fontFamily) textStyle.fontFamily = style.fontFamily
    if (style.fontStyle) textStyle.fontStyle = style.fontStyle

    const text = this.add.text(x, y, `+${amount} XP`, textStyle)
      .setOrigin(0.5)
      .setDepth(DEPTH.FEEDBACK)

    const reducePestMotion = this.reduceMotion && source === 'PEST_REMOVE'

    this.tweens.add({
      targets: text,
      y: y - (reducePestMotion ? 10 : 30),
      alpha: 0,
      scale: reducePestMotion ? 1 : style.scale,
      duration: reducePestMotion ? 500 : 800,
      ease: 'Cubic.easeOut',
      onComplete: () => text.destroy(),
    })
  }
}
