import { useCallback, useEffect, useState } from 'react'
import { authFetch } from '../api/http'
import type { ShopItem } from '../types/Shop'

export function useShopItems() {
  const [items, setItems] = useState<ShopItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refreshItems = useCallback(async () => {
    setLoading(true)

    try {
      const nextItems = await authFetch<ShopItem[]>('/shop/items')
      setItems(nextItems)
      setError(null)
    } catch (requestError) {
      setError(
        requestError instanceof Error
          ? requestError.message
          : 'Não foi possível carregar os itens da loja.',
      )
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void refreshItems()
  }, [refreshItems])

  function getItem(id?: string) {
    return items.find(item => item.id === id)
  }

  return {
    items,
    loading,
    error,
    refreshItems,
    getItem,
  }
}
