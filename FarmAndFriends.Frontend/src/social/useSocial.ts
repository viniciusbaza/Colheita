import { useContext } from 'react'
import { SocialContext } from './SocialContext'

export function useSocial() {
  const context = useContext(SocialContext)

  if (!context) {
    throw new Error('useSocial must be used within SocialProvider')
  }

  return context
}
