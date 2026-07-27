import { useContext } from 'react'
import { FarmContext } from './FarmContextValue'

export function useFarm() {
  const context = useContext(FarmContext)

  if (!context) {
    throw new Error('useFarm must be used inside FarmProvider')
  }

  return context
}
