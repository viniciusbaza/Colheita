import { useEffect, useState } from 'react'
import type { Seed } from '../types/Farm'
import { authFetch } from '../api/http'

export function useSeeds() {
  const [seeds, setSeeds] = useState<Seed[]>([])

  useEffect(() => {
    authFetch<Seed[]>('/seeds')
      .then(setSeeds)
      .catch(console.error)
  }, [])

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
    getSeed,
    getSeedByItem 
  }
}
