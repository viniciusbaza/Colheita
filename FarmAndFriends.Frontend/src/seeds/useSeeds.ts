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

  return {
    seeds,
    getSeed
  }
}
