import { FarmContext } from './FarmContextValue'
import { useFarmInternal } from './useFarm'

export function FarmProvider({ children }: { children: React.ReactNode }) {
  const farm = useFarmInternal()
  return (
    <FarmContext.Provider value={farm}>
      {children}
    </FarmContext.Provider>
  )
}
