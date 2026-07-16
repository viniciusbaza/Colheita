import { useEffect, useState } from 'react'
import { authFetch } from '../api/http'
import type { Inventory } from '../types/Inventory'

export function useInventory() {
  const [inventory, setInventory] = useState<Inventory | null>(null)
  const [loading, setLoading] = useState(true)

  async function fetchInventory() {
    try {
      const data = await authFetch<Inventory>('/inventory')
      setInventory(data)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchInventory()

    function onInventoryChanged() {
      fetchInventory()
    }

    window.addEventListener('inventory:changed', onInventoryChanged)

    return () => {
      window.removeEventListener('inventory:changed', onInventoryChanged)
    }
  }, [])

  return {
    inventory,
    loading,
    refreshInventory: fetchInventory,
  }
}
