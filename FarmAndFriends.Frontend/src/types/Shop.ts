export type ShopItem = {
  id: string
  name: string
  icon: string
  description: string
  buyPrice: number
  minLevel: number
  protectionDurationHours: number
}

export type BuyItemResponse = {
  itemId: string
  quantity: number
  inventoryQuantity: number
  coinsLeft: number
}
