import { createContext } from 'react'
import type { Friend } from '../types/Friend'
import type {
  FriendRequest,
  SocialNotification,
  UserSearchResult,
} from '../types/Social'

export type SocialContextValue = {
  friends: Friend[]
  incomingRequests: FriendRequest[]
  outgoingRequests: FriendRequest[]
  notifications: SocialNotification[]
  unreadCount: number
  loading: boolean
  error: string | null
  toastNotification: SocialNotification | null
  refreshSocial: () => Promise<void>
  searchUsers: (query: string) => Promise<UserSearchResult[]>
  sendRequest: (recipientUserId: string) => Promise<void>
  acceptRequest: (friendshipId: string) => Promise<void>
  declineRequest: (friendshipId: string) => Promise<void>
  cancelRequest: (friendshipId: string) => Promise<void>
  removeFriend: (friendUserId: string) => Promise<void>
  markNotificationAsRead: (notificationId: string) => Promise<void>
  markAllNotificationsAsRead: () => Promise<void>
  dismissToast: () => void
}

export const SocialContext = createContext<SocialContextValue | null>(null)
