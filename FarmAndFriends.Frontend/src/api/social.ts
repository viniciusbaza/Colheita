import { authFetch } from './http'
import type { Friend } from '../types/Friend'
import type {
  FriendRequest,
  SocialNotification,
  UnreadCountResponse,
  UserSearchResult,
} from '../types/Social'

export function getFriends() {
  return authFetch<Friend[]>('/friends')
}

export function getIncomingFriendRequests() {
  return authFetch<FriendRequest[]>('/friends/requests/incoming')
}

export function getOutgoingFriendRequests() {
  return authFetch<FriendRequest[]>('/friends/requests/outgoing')
}

export function searchUsers(query: string, take = 20) {
  const params = new URLSearchParams({
    q: query,
    take: String(take),
  })

  return authFetch<UserSearchResult[]>(`/users/search?${params.toString()}`)
}

export function sendFriendRequest(recipientUserId: string) {
  return authFetch<FriendRequest>('/friends/requests', {
    method: 'POST',
    body: JSON.stringify({ recipientUserId }),
  })
}

export function acceptFriendRequest(friendshipId: string) {
  return authFetch<FriendRequest>(`/friends/requests/${friendshipId}/accept`, {
    method: 'POST',
  })
}

export function declineFriendRequest(friendshipId: string) {
  return authFetch<FriendRequest>(`/friends/requests/${friendshipId}/decline`, {
    method: 'POST',
  })
}

export function cancelFriendRequest(friendshipId: string) {
  return authFetch<void>(`/friends/requests/${friendshipId}`, {
    method: 'DELETE',
  })
}

export function removeFriend(friendUserId: string) {
  return authFetch<void>(`/friends/${friendUserId}`, {
    method: 'DELETE',
  })
}

export function getNotifications(unreadOnly = false, take = 50) {
  const params = new URLSearchParams({
    unreadOnly: String(unreadOnly),
    take: String(take),
  })

  return authFetch<SocialNotification[]>(`/notifications?${params.toString()}`)
}

export function getUnreadNotifications(take = 50) {
  return getNotifications(true, take)
}

export function getUnreadNotificationCount() {
  return authFetch<UnreadCountResponse>('/notifications/unread-count')
}

export function markNotificationAsRead(notificationId: string) {
  return authFetch<void>(`/notifications/${notificationId}/read`, {
    method: 'PATCH',
  })
}

export function markAllNotificationsAsRead() {
  return authFetch<void>('/notifications/read-all', {
    method: 'PATCH',
  })
}
