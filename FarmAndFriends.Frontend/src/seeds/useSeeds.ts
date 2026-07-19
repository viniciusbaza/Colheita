import { useCallback, useEffect, useState } from 'react'
import type { Seed } from '../types/Farm'
import { authFetch } from '../api/http'

export function useSeeds() {
  const [seeds, setSeeds] = useState<Seed[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const refreshSeeds = useCallback(async () => {
    setLoading(true)

    try {
      const nextSeeds = await authFetch<Seed[]>('/seeds')
      setSeeds(nextSeeds)
      setError(null)
    } catch (requestError) {
      setError(
        requestError instanceof Error
          ? requestError.message
          : 'Não foi possível carregar o catálogo de sementes.',
      )
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void refreshSeeds()
  }, [refreshSeeds])

  function getSeed(id?: string) {
    return seeds.find(s => s.id === id)
  }

  function getSeedByItem(itemType?: 'Seed' | 'Crop', itemId?: string) {
    if (!itemType || !itemId) return undefined

    if (itemType === 'Seed') {
      return seeds.find(s => s.id === itemId)
    }

    if (itemType === 'Crop') {
      return seeds.find(s => s.cropId === itemId)
    }

    return undefined
  }

  return {
    seeds,
    loading,
    error,
    refreshSeeds,
    getSeed,
    getSeedByItem 
  }
}
