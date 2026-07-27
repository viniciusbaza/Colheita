import { createContext } from 'react'
import type { useFarmInternal } from './useFarm'

export const FarmContext = createContext<
  ReturnType<typeof useFarmInternal> | null
>(null)
