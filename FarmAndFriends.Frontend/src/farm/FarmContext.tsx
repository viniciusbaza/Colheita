import { createContext, useContext } from 'react'
import { useFarmInternal } from './useFarm'

const FarmContext = createContext<
  ReturnType<typeof useFarmInternal> | null
>(null)

export function FarmProvider({ children }: { children: React.ReactNode }) {
  const farm = useFarmInternal()
  return (
    <FarmContext.Provider value={farm}>
      {children}
    </FarmContext.Provider>
  )
}

export function useFarm() {
  const ctx = useContext(FarmContext)
  if (!ctx) {
    throw new Error('useFarm must be used inside FarmProvider')
  }
  return ctx
}
