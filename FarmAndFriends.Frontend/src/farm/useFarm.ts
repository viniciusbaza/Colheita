import { useEffect, useState } from 'react'
import { authFetch } from '../api/http'
import { type Farm } from '../types/Farm'

export function useFarm() {
  const [farm, setFarm] = useState<Farm | null>(null)
  const [loading, setLoading] = useState(true)

  async function fetchFarm() {  
    try {
      const data = await authFetch<Farm>('/farms/my')
      setFarm(data)

      // 🔥 avisa o Phaser com a fonte de verdade
      window.dispatchEvent(
        new CustomEvent('farm:sync', {
          detail: data
        })
      )
    } catch (err) {
      console.warn('Sessão expirada, redirecionando...')
      setFarm(null)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchFarm()
  }, [])

  useEffect(() => {
    if (!farm) return

    const hasGrowing = farm.plots.some(
      p => p.seedId && !p.isReady
    )

    if (!hasGrowing) return

    const interval = setInterval(() => {
      fetchFarm()
    }, 10_000) // a cada 10 segundos

    return () => clearInterval(interval)
  }, [farm?.id])

  return { farm, loading, refreshFarm: fetchFarm }
}
