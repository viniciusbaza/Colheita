import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import * as socialApi from '../api/social'
import { useAuth } from '../auth/useAuth'
import type { Friend } from '../types/Friend'
import type {
  FriendRequest,
  SocialNotification,
  UserSearchResult,
} from '../types/Social'
import { SocialContext } from './SocialContext'

const POLLING_INTERVAL_MS = 30_000
const TOASTED_NOTIFICATIONS_KEY = 'social_toasted_notification_ids'

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Erro ao carregar dados sociais.'
}

function readToastedNotificationIds() {
  try {
    const stored = sessionStorage.getItem(TOASTED_NOTIFICATIONS_KEY)
    const parsed = stored ? JSON.parse(stored) : []
    return new Set<string>(Array.isArray(parsed) ? parsed : [])
  } catch {
    return new Set<string>()
  }
}

export function SocialProvider({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  const [friends, setFriends] = useState<Friend[]>([])
  const [incomingRequests, setIncomingRequests] = useState<FriendRequest[]>([])
  const [outgoingRequests, setOutgoingRequests] = useState<FriendRequest[]>([])
  const [notifications, setNotifications] = useState<SocialNotification[]>([])
  const [unreadCount, setUnreadCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [toastNotification, setToastNotification] =
    useState<SocialNotification | null>(null)
  const toastedNotificationIds = useRef(readToastedNotificationIds())
  const refreshInProgress = useRef<Promise<void> | null>(null)

  const registerNotifications = useCallback((next: SocialNotification[]) => {
    const unseenNotifications = next.filter(
      notification =>
        notification.readAt == null &&
        !toastedNotificationIds.current.has(notification.id),
    )
    const nextToast = unseenNotifications[0]

    if (unseenNotifications.length > 0) {
      for (const notification of unseenNotifications) {
        toastedNotificationIds.current.add(notification.id)
      }

      const storedIds = [...toastedNotificationIds.current].slice(-100)
      sessionStorage.setItem(
        TOASTED_NOTIFICATIONS_KEY,
        JSON.stringify(storedIds),
      )
    }

    if (nextToast) {
      setToastNotification(nextToast)
    }

    setNotifications(next)
  }, [])

  const refreshSocial = useCallback(async () => {
    if (!token) return

    if (refreshInProgress.current) {
      return refreshInProgress.current
    }

    const refresh = (async () => {
      try {
        const [
          nextFriends,
          nextIncoming,
          nextOutgoing,
          nextNotifications,
          unread,
        ] = await Promise.all([
          socialApi.getFriends(),
          socialApi.getIncomingFriendRequests(),
          socialApi.getOutgoingFriendRequests(),
          socialApi.getNotifications(false, 50),
          socialApi.getUnreadNotificationCount(),
        ])

        setFriends(nextFriends)
        setIncomingRequests(nextIncoming)
        setOutgoingRequests(nextOutgoing)
        registerNotifications(nextNotifications)
        setUnreadCount(unread.count)
        setError(null)
      } catch (refreshError) {
        setError(getErrorMessage(refreshError))
        throw refreshError
      } finally {
        setLoading(false)
        refreshInProgress.current = null
      }
    })()

    refreshInProgress.current = refresh
    return refresh
  }, [registerNotifications, token])

  useEffect(() => {
    if (!token) {
      setFriends([])
      setIncomingRequests([])
      setOutgoingRequests([])
      setNotifications([])
      setUnreadCount(0)
      setToastNotification(null)
      setLoading(false)
      return
    }

    setLoading(true)
    void refreshSocial().catch(() => undefined)

    const intervalId = window.setInterval(() => {
      void refreshSocial().catch(() => undefined)
    }, POLLING_INTERVAL_MS)

    function refreshWhenVisible() {
      if (document.visibilityState === 'visible') {
        void refreshSocial().catch(() => undefined)
      }
    }

    window.addEventListener('focus', refreshWhenVisible)
    document.addEventListener('visibilitychange', refreshWhenVisible)

    return () => {
      window.clearInterval(intervalId)
      window.removeEventListener('focus', refreshWhenVisible)
      document.removeEventListener('visibilitychange', refreshWhenVisible)
    }
  }, [refreshSocial, token])

  const refreshAfterMutation = useCallback(async () => {
    try {
      await refreshSocial()
    } catch {
      // The mutation already succeeded; the next poll will reconcile the UI.
    }
  }, [refreshSocial])

  const searchUsers = useCallback((query: string): Promise<UserSearchResult[]> => {
    return socialApi.searchUsers(query)
  }, [])

  const sendRequest = useCallback(async (recipientUserId: string) => {
    await socialApi.sendFriendRequest(recipientUserId)
    await refreshAfterMutation()
  }, [refreshAfterMutation])

  const acceptRequest = useCallback(async (friendshipId: string) => {
    await socialApi.acceptFriendRequest(friendshipId)
    await refreshAfterMutation()
  }, [refreshAfterMutation])

  const declineRequest = useCallback(async (friendshipId: string) => {
    await socialApi.declineFriendRequest(friendshipId)
    await refreshAfterMutation()
  }, [refreshAfterMutation])

  const cancelRequest = useCallback(async (friendshipId: string) => {
    await socialApi.cancelFriendRequest(friendshipId)
    await refreshAfterMutation()
  }, [refreshAfterMutation])

  const removeFriend = useCallback(async (friendUserId: string) => {
    await socialApi.removeFriend(friendUserId)
    await refreshAfterMutation()
  }, [refreshAfterMutation])

  const markNotificationAsRead = useCallback(async (notificationId: string) => {
    await socialApi.markNotificationAsRead(notificationId)
    const readAt = new Date().toISOString()
    setNotifications(current =>
      current.map(notification =>
        notification.id === notificationId
          ? { ...notification, readAt }
          : notification,
      ),
    )
    setUnreadCount(current => Math.max(0, current - 1))
  }, [])

  const markAllNotificationsAsRead = useCallback(async () => {
    await socialApi.markAllNotificationsAsRead()

    const readAt = new Date().toISOString()
    setNotifications(current =>
      current.map(notification =>
        notification.readAt == null
          ? { ...notification, readAt }
          : notification,
      ),
    )
    setUnreadCount(0)
  }, [])

  const dismissToast = useCallback(() => {
    setToastNotification(null)
  }, [])

  const value = useMemo(() => ({
    friends,
    incomingRequests,
    outgoingRequests,
    notifications,
    unreadCount,
    loading,
    error,
    toastNotification,
    refreshSocial,
    searchUsers,
    sendRequest,
    acceptRequest,
    declineRequest,
    cancelRequest,
    removeFriend,
    markNotificationAsRead,
    markAllNotificationsAsRead,
    dismissToast,
  }), [
    acceptRequest,
    cancelRequest,
    declineRequest,
    dismissToast,
    error,
    friends,
    incomingRequests,
    loading,
    markNotificationAsRead,
    markAllNotificationsAsRead,
    notifications,
    outgoingRequests,
    refreshSocial,
    removeFriend,
    searchUsers,
    sendRequest,
    toastNotification,
    unreadCount,
  ])

  return (
    <SocialContext.Provider value={value}>
      {children}
    </SocialContext.Provider>
  )
}
