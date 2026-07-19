import { authFetch } from '../api/http'

export const MAX_SHOP_QUANTITY = 10_000

type BuySeedResponse = {
  seedId: string
  quantity: number
  coinsLeft: number
}

type SellCropResponse = {
  crop: string
  sold: number
  earned: number
  coins: number
}

export function useShop() {
  function buySeed(seedId: string, quantity: number) {
    return authFetch<BuySeedResponse>('/shop/buy-seed', {
      method: 'POST',
      body: JSON.stringify({ seedId, quantity }),
    })
  }

  function sellCrop(cropId: string, quantity: number) {
    return authFetch<SellCropResponse>('/shop/sell-crop', {
      method: 'POST',
      body: JSON.stringify({ cropId, quantity }),
    })
  }

  return {
    buySeed,
    sellCrop,
  }
}
