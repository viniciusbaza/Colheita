import { useEffect, useMemo, useState } from 'react'
import { useFarm } from '../farm/useFarmContext'
import { useSocial } from '../social/useSocial'
import {
  NotificationType,
  type SocialNotification,
} from '../types/Social'
import {
  getNotificationAction,
  getNotificationCategory,
  type NotificationCategory,
} from '../utils/notificationFeed'
import {
  getNotificationIcon,
  getNotificationMessage,
  getNotificationTitle,
} from '../utils/notifications'

type NotificationsPanelProps = {
  onClose: () => void
  onOpenFriendRequests: () => void
}

type ReadSection = {
  key: 'unread' | 'read'
  title: string
  notifications: SocialNotification[]
}

function formatNotificationTime(value: string) {
  return new Intl.DateTimeFormat('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

function getDateKey(value: string) {
  const date = new Date(value)
  return `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`
}

function formatDay(value: string) {
  const date = new Date(value)
  const today = new Date()
  const yesterday = new Date(today)
  yesterday.setDate(today.getDate() - 1)

  if (getDateKey(value) === getDateKey(today.toISOString())) {
    return 'Hoje'
  }

  if (getDateKey(value) === getDateKey(yesterday.toISOString())) {
    return 'Ontem'
  }

  return new Intl.DateTimeFormat('pt-BR', {
    weekday: 'long',
    day: '2-digit',
    month: 'long',
  }).format(date)
}

function getErrorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'Não foi possível atualizar os acontecimentos.'
}

function getCardClass(notification: SocialNotification, isUnread: boolean) {
  if (notification.type === NotificationType.TheftOccurred) {
    return isUnread
      ? 'border-orange-300 bg-orange-50'
      : 'border-orange-100 bg-orange-50/60'
  }

  return isUnread
    ? 'border-amber-300 bg-amber-50'
    : 'border-transparent bg-white/80'
}

function groupByDay(notifications: SocialNotification[]) {
  const groups = new Map<string, SocialNotification[]>()

  for (const notification of notifications) {
    const key = getDateKey(notification.createdAt)
    const group = groups.get(key)

    if (group) {
      group.push(notification)
    } else {
      groups.set(key, [notification])
    }
  }

  return [...groups.values()]
}

export function NotificationsPanel({
  onClose,
  onOpenFriendRequests,
}: NotificationsPanelProps) {
  const { visitFarm } = useFarm()
  const {
    friends,
    incomingRequests,
    notifications,
    unreadCount,
    hasMoreNotifications,
    loadingMoreNotifications,
    loading,
    error,
    loadMoreNotifications,
    markNotificationAsRead,
    markAllNotificationsAsRead,
  } = useSocial()
  const [activeCategory, setActiveCategory] =
    useState<NotificationCategory>('farm')
  const [busyNotificationIds, setBusyNotificationIds] = useState<Set<string>>(
    () => new Set(),
  )
  const [markingAll, setMarkingAll] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  useEffect(() => {
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }

    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [onClose])

  const categoryNotifications = useMemo(
    () => notifications.filter(
      notification => getNotificationCategory(notification) === activeCategory,
    ),
    [activeCategory, notifications],
  )

  const pendingIncomingRequestIds = useMemo(
    () => new Set(incomingRequests.map(request => request.id)),
    [incomingRequests],
  )

  const readSections = useMemo<ReadSection[]>(() => [
    {
      key: 'unread',
      title: 'Novos',
      notifications: categoryNotifications.filter(
        notification => notification.readAt == null,
      ),
    },
    {
      key: 'read',
      title: 'Já vistos',
      notifications: categoryNotifications.filter(
        notification => notification.readAt != null,
      ),
    },
  ], [categoryNotifications])

  async function markAsRead(notification: SocialNotification) {
    if (notification.readAt || busyNotificationIds.has(notification.id)) return

    setBusyNotificationIds(current => new Set(current).add(notification.id))
    setActionError(null)

    try {
      await markNotificationAsRead(notification.id)
    } catch (readError) {
      setActionError(getErrorMessage(readError))
    } finally {
      setBusyNotificationIds(current => {
        const next = new Set(current)
        next.delete(notification.id)
        return next
      })
    }
  }

  async function markAllAsRead() {
    if (unreadCount === 0 || markingAll) return

    setMarkingAll(true)
    setActionError(null)

    try {
      await markAllNotificationsAsRead()
    } catch (readError) {
      setActionError(getErrorMessage(readError))
    } finally {
      setMarkingAll(false)
    }
  }

  async function loadMore() {
    if (!hasMoreNotifications || loadingMoreNotifications) return

    setActionError(null)

    try {
      await loadMoreNotifications()
    } catch (loadError) {
      setActionError(getErrorMessage(loadError))
    }
  }

  function runContextualAction(notification: SocialNotification) {
    const action = getNotificationAction(
      notification,
      friends,
      pendingIncomingRequestIds,
    )
    if (!action) return

    if (notification.readAt == null) {
      void markNotificationAsRead(notification.id).catch(() => undefined)
    }

    if (action.kind === 'openFriendRequests') {
      onClose()
      onOpenFriendRequests()
      return
    }

    visitFarm(
      action.friend.farmId,
      action.friend.userId,
      action.friend.username,
    )
    onClose()
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-3 text-emerald-950"
      onClick={onClose}
      onPointerDown={event => event.stopPropagation()}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="events-panel-title"
        className="flex max-h-[88vh] w-full max-w-xl flex-col overflow-hidden rounded-2xl bg-emerald-50 shadow-2xl"
        onClick={event => event.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-2 bg-emerald-800 px-4 py-3 text-white sm:items-center sm:gap-3">
          <div className="min-w-0">
            <h2 id="events-panel-title" className="text-lg font-bold">
              🔔 Acontecimentos
            </h2>
            <p className="text-xs text-emerald-100">
              Veja quem passou por aqui e o que aconteceu
            </p>
          </div>
          <div className="flex items-center gap-2">
            {unreadCount > 0 && (
              <button
                type="button"
                onClick={() => void markAllAsRead()}
                disabled={markingAll}
                className="rounded-lg bg-white/15 px-2 py-2 text-xs font-semibold hover:bg-white/25 disabled:opacity-60 sm:px-3"
              >
                <span className="sm:hidden">
                  {markingAll ? 'Atualizando...' : 'Marcar tudo'}
                </span>
                <span className="hidden sm:inline">
                  {markingAll ? 'Atualizando...' : 'Marcar tudo como visto'}
                </span>
              </button>
            )}
            <button
              type="button"
              onClick={onClose}
              className="rounded-full px-3 py-1 text-xl hover:bg-white/15 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white"
              aria-label="Fechar painel de acontecimentos"
            >
              ×
            </button>
          </div>
        </div>

        <div
          role="tablist"
          aria-label="Categorias de acontecimentos"
          className="grid grid-cols-2 border-b border-emerald-200 bg-white/70 p-2"
        >
          <button
            id="events-farm-tab"
            type="button"
            role="tab"
            aria-selected={activeCategory === 'farm'}
            aria-controls="events-tab-panel"
            onClick={() => setActiveCategory('farm')}
            className={`min-h-11 rounded-lg px-3 py-2 text-sm font-bold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600 ${
              activeCategory === 'farm'
                ? 'bg-emerald-700 text-white shadow'
                : 'text-emerald-800 hover:bg-emerald-100'
            }`}
          >
            🌾 Sua fazenda
          </button>
          <button
            id="events-friendships-tab"
            type="button"
            role="tab"
            aria-selected={activeCategory === 'friendships'}
            aria-controls="events-tab-panel"
            onClick={() => setActiveCategory('friendships')}
            className={`min-h-11 rounded-lg px-3 py-2 text-sm font-bold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600 ${
              activeCategory === 'friendships'
                ? 'bg-emerald-700 text-white shadow'
                : 'text-emerald-800 hover:bg-emerald-100'
            }`}
          >
            👥 Amizades
          </button>
        </div>

        {(error || actionError) && (
          <div
            role="alert"
            className="mx-3 mt-3 rounded-lg border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-700"
          >
            {actionError ?? error}
          </div>
        )}

        <div
          id="events-tab-panel"
          role="tabpanel"
          aria-labelledby={
            activeCategory === 'farm'
              ? 'events-farm-tab'
              : 'events-friendships-tab'
          }
          className="min-h-0 flex-1 overflow-y-auto p-3"
        >
          {loading ? (
            <div className="flex h-full min-h-32 items-center justify-center text-sm text-emerald-700">
              Carregando acontecimentos...
            </div>
          ) : categoryNotifications.length === 0 ? (
            <div className="py-8 text-center text-emerald-700 sm:py-14">
              <p className="text-4xl" aria-hidden="true">
                {activeCategory === 'farm' ? '🌾' : '👋'}
              </p>
              <p className="mt-3 font-semibold">
                Nenhum acontecimento por enquanto.
              </p>
              <p className="mt-1 text-sm">
                {activeCategory === 'farm'
                  ? 'As histórias da sua fazenda aparecerão aqui.'
                  : 'As novidades das suas amizades aparecerão aqui.'}
              </p>
            </div>
          ) : (
            <div className="space-y-5">
              {readSections.map(section => section.notifications.length > 0 && (
                <section key={section.key} aria-labelledby={`events-${section.key}`}>
                  <h3
                    id={`events-${section.key}`}
                    className="mb-2 text-sm font-extrabold uppercase tracking-wide text-emerald-800"
                  >
                    {section.title}
                  </h3>

                  <div className="space-y-4">
                    {groupByDay(section.notifications).map(dayNotifications => (
                      <div key={getDateKey(dayNotifications[0].createdAt)}>
                        <p className="mb-1.5 text-xs font-bold capitalize text-emerald-600">
                          {formatDay(dayNotifications[0].createdAt)}
                        </p>
                        <div className="space-y-2">
                          {dayNotifications.map(notification => {
                            const isUnread = notification.readAt == null
                            const isBusy = busyNotificationIds.has(notification.id)
                            const action = getNotificationAction(
                              notification,
                              friends,
                              pendingIncomingRequestIds,
                            )

                            return (
                              <article
                                key={notification.id}
                                className={`relative flex gap-3 rounded-xl border p-3 shadow-sm transition ${getCardClass(notification, isUnread)}`}
                              >
                                <span
                                  className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-xl"
                                  aria-hidden="true"
                                >
                                  {getNotificationIcon(notification)}
                                </span>
                                <div className="min-w-0 flex-1">
                                  <div className="flex items-start justify-between gap-3">
                                    <div>
                                      <h4 className="font-semibold">
                                        {getNotificationTitle(notification)}
                                      </h4>
                                      <p className="mt-0.5 text-sm text-emerald-900">
                                        {getNotificationMessage(notification)}
                                      </p>
                                    </div>
                                    {isUnread && (
                                      <span
                                        className="mt-1 h-2.5 w-2.5 shrink-0 rounded-full bg-red-500"
                                        aria-label="Novo"
                                      />
                                    )}
                                  </div>
                                  <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
                                    <time
                                      dateTime={notification.createdAt}
                                      className="text-xs text-emerald-600"
                                    >
                                      {formatNotificationTime(notification.createdAt)}
                                    </time>
                                    <div className="flex flex-wrap items-center justify-end gap-2">
                                      {action && (
                                        <button
                                          type="button"
                                          onClick={() => runContextualAction(notification)}
                                          className="min-h-9 rounded-lg bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800 hover:bg-emerald-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600"
                                        >
                                          {action.kind === 'visit'
                                            ? `Visitar`
                                            : 'Ver solicitações'}
                                        </button>
                                      )}
                                      {isUnread && (
                                        <button
                                          type="button"
                                          onClick={() => void markAsRead(notification)}
                                          disabled={isBusy}
                                          className="min-h-9 rounded-lg px-3 py-1 text-xs font-semibold text-emerald-700 hover:bg-emerald-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600 disabled:opacity-60"
                                        >
                                          {isBusy ? 'Atualizando...' : 'Marcar como visto'}
                                        </button>
                                      )}
                                    </div>
                                  </div>
                                </div>
                              </article>
                            )
                          })}
                        </div>
                      </div>
                    ))}
                  </div>
                </section>
              ))}
            </div>
          )}

          {!loading && hasMoreNotifications && (
            <div className="mt-4 flex justify-center">
              <button
                type="button"
                onClick={() => void loadMore()}
                disabled={loadingMoreNotifications}
                className="min-h-11 rounded-xl border border-emerald-300 bg-white px-4 py-2 text-sm font-bold text-emerald-800 shadow-sm hover:bg-emerald-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600 disabled:opacity-60"
              >
                {loadingMoreNotifications ? 'Carregando...' : 'Carregar mais'}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
