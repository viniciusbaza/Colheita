import Phaser from 'phaser'

export default class MainScene extends Phaser.Scene {
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