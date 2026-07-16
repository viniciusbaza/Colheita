import { authFetch } from '../api/http'

export function useShop() {
  async function buySeed(seedId: string, quantity: number) {
    try {
        return authFetch('/shop/buy-seed', {
        method: 'POST',
        body: JSON.stringify({ seedId, quantity })
        })
    } catch (err: any) {
        throw err
    }
  }

  async function sellCrop(cropId: string, quantity: number) {
    try {
        return authFetch('/shop/sell-crop', {
        method: 'POST',
        body: JSON.stringify({ cropId, quantity })
        })
    } catch (err: any) {
        throw err
    }
  }

  return {
    buySeed,
    sellCrop
  }
}
