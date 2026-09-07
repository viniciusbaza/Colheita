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
import { mergeNotificationFeedItems } from '../utils/notificationFeed'
import {
  advanceAsyncSession,
  isAsyncSessionCurrent,
  type AsyncSession,
} from './asyncSession'
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
  const [hasMoreNotifications, setHasMoreNotifications] = useState(false)
  const [loadingMoreNotifications, setLoadingMoreNotifications] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [toastNotification, setToastNotification] =
    useState<SocialNotification | null>(null)
  const toastedNotificationIds = useRef(readToastedNotificationIds())
  const refreshInProgress = useRef<Promise<void> | null>(null)
  const nextNotificationCursor = useRef<string | null>(null)
  const loadedAdditionalFeedPages = useRef(false)
  const loadMoreInProgress = useRef<Promise<void> | null>(null)
  const activeSession = useRef<AsyncSession>({ token, generation: 0 })
  activeSession.current = advanceAsyncSession(activeSession.current, token)
  const [dataSession, setDataSession] = useState(activeSession.current)

  const registerFirstFeedPage = useCallback((
    next: SocialNotification[],
    nextCursor: string | null,
    windowStartUtc: string,
  ) => {
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

    setNotifications(current =>
      mergeNotificationFeedItems(current, next, windowStartUtc),
    )

    if (!loadedAdditionalFeedPages.current) {
      nextNotificationCursor.current = nextCursor
      setHasMoreNotifications(nextCursor != null)
    }
  }, [])

  const refreshSocial = useCallback(async () => {
    if (!token) return

    const session = activeSession.current
    if (session.token !== token) return

    if (refreshInProgress.current) {
      return refreshInProgress.current
    }

    const refresh = (async () => {
      try {
        const [
          nextFriends,
          nextIncoming,
          nextOutgoing,
          nextFeed,
        ] = await Promise.all([
          socialApi.getFriends(),
          socialApi.getIncomingFriendRequests(),
          socialApi.getOutgoingFriendRequests(),
          socialApi.getNotificationFeed(undefined, 50),
        ])

        if (!isAsyncSessionCurrent(activeSession.current, session)) return

        setDataSession(session)
        setFriends(nextFriends)
        setIncomingRequests(nextIncoming)
        setOutgoingRequests(nextOutgoing)
        registerFirstFeedPage(
          nextFeed.items,
          nextFeed.nextCursor,
          nextFeed.windowStartUtc,
        )
        setUnreadCount(nextFeed.unreadCount)
        setError(null)
      } catch (refreshError) {
        if (isAsyncSessionCurrent(activeSession.current, session)) {
          setError(getErrorMessage(refreshError))
        }
        throw refreshError
      } finally {
        if (isAsyncSessionCurrent(activeSession.current, session)) {
          setLoading(false)
          refreshInProgress.current = null
        }
      }
    })()

    refreshInProgress.current = refresh
    return refresh
  }, [registerFirstFeedPage, token])

  useEffect(() => {
    setDataSession(activeSession.current)
    setFriends([])
    setIncomingRequests([])
    setOutgoingRequests([])
    setNotifications([])
    setUnreadCount(0)
    nextNotificationCursor.current = null
    loadedAdditionalFeedPages.current = false
    setHasMoreNotifications(false)
    setLoadingMoreNotifications(false)
    setToastNotification(null)
    setError(null)
    refreshInProgress.current = null
    loadMoreInProgress.current = null

    if (!token) {
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
      refreshInProgress.current = null
      loadMoreInProgress.current = null
    }
  }, [refreshSocial, token])

  const refreshAfterMutation = useCallback(async () => {
    try {
      await refreshSocial()
    } catch {
      // The mutation already succeeded; the next poll will reconcile the UI.
    }
  }, [refreshSocial])

  const loadMoreNotifications = useCallback(async () => {
    if (!token || !nextNotificationCursor.current) return

    const session = activeSession.current
    if (session.token !== token) return

    if (loadMoreInProgress.current) {
      return loadMoreInProgress.current
    }

    const cursor = nextNotificationCursor.current
    const loadMore = (async () => {
      setLoadingMoreNotifications(true)

      try {
        const nextFeed = await socialApi.getNotificationFeed(cursor, 50)
        if (!isAsyncSessionCurrent(activeSession.current, session)) return

        loadedAdditionalFeedPages.current = true
        nextNotificationCursor.current = nextFeed.nextCursor
        setHasMoreNotifications(nextFeed.nextCursor != null)
        setUnreadCount(nextFeed.unreadCount)
        setNotifications(current =>
          mergeNotificationFeedItems(
            current,
            nextFeed.items,
            nextFeed.windowStartUtc,
          ),
        )
        setError(null)
      } catch (loadError) {
        if (isAsyncSessionCurrent(activeSession.current, session)) {
          setError(getErrorMessage(loadError))
        }
        throw loadError
      } finally {
        if (isAsyncSessionCurrent(activeSession.current, session)) {
          setLoadingMoreNotifications(false)
          loadMoreInProgress.current = null
        }
      }
    })()

    loadMoreInProgress.current = loadMore
    return loadMore
  }, [token])

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
    const session = activeSession.current
    await socialApi.markNotificationAsRead(notificationId)
    if (!isAsyncSessionCurrent(activeSession.current, session)) return

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
    const session = activeSession.current
    await socialApi.markAllNotificationsAsRead()
    if (!isAsyncSessionCurrent(activeSession.current, session)) return

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

  const hasCurrentSessionData = isAsyncSessionCurrent(
    activeSession.current,
    dataSession,
  )

  const value = useMemo(() => ({
    friends: hasCurrentSessionData ? friends : [],
    incomingRequests: hasCurrentSessionData ? incomingRequests : [],
    outgoingRequests: hasCurrentSessionData ? outgoingRequests : [],
    notifications: hasCurrentSessionData ? notifications : [],
    unreadCount: hasCurrentSessionData ? unreadCount : 0,
    hasMoreNotifications: hasCurrentSessionData && hasMoreNotifications,
    loadingMoreNotifications:
      hasCurrentSessionData && loadingMoreNotifications,
    loading: token ? !hasCurrentSessionData || loading : false,
    error: hasCurrentSessionData ? error : null,
    toastNotification: hasCurrentSessionData ? toastNotification : null,
    refreshSocial,
    searchUsers,
    sendRequest,
    acceptRequest,
    declineRequest,
    cancelRequest,
    removeFriend,
    markNotificationAsRead,
    markAllNotificationsAsRead,
    loadMoreNotifications,
    dismissToast,
  }), [
    acceptRequest,
    cancelRequest,
    declineRequest,
    dismissToast,
    error,
    friends,
    hasCurrentSessionData,
    hasMoreNotifications,
    incomingRequests,
    loading,
    loadingMoreNotifications,
    loadMoreNotifications,
    markNotificationAsRead,
    markAllNotificationsAsRead,
    notifications,
    outgoingRequests,
    refreshSocial,
    removeFriend,
    searchUsers,
    sendRequest,
    toastNotification,
    token,
    unreadCount,
  ])

  return (
    <SocialContext.Provider value={value}>
      {children}
    </SocialContext.Provider>
  )
}
