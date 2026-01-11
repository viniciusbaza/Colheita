export type InventoryItem = {
  itemType: 'Seed' | 'Crop'
  itemId: string
  quantity: number
}

export type Inventory = {
  coins: number
  premiumCoins: number
  items: InventoryItem[]
}
