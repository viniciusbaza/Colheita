import { useEffect, useState } from 'react'
import { useFarm } from '../farm/useFarmContext'
import { useSocial } from '../social/useSocial'
import {
  NotificationType,
  type SocialNotification,
} from '../types/Social'
import {
  getNotificationIcon,
  getNotificationMessage,
  getNotificationTitle,
} from '../utils/notifications'

type NotificationsPanelProps = {
  onClose: () => void
}

function formatNotificationDate(value: string) {
  return new Intl.DateTimeFormat('pt-BR', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value))
}

function getErrorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'Não foi possível atualizar as notificações.'
}

export function NotificationsPanel({ onClose }: NotificationsPanelProps) {
  const { visitFarm } = useFarm()
  const {
    friends,
    notifications,
    unreadCount,
    loading,
    error,
    markNotificationAsRead,
    markAllNotificationsAsRead,
  } = useSocial()
  const [busyNotificationId, setBusyNotificationId] = useState<string | null>(null)
  const [markingAll, setMarkingAll] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  useEffect(() => {
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }

    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [onClose])

  async function markAsRead(notification: SocialNotification) {
    if (notification.readAt || busyNotificationId) return

    setBusyNotificationId(notification.id)
    setActionError(null)

    try {
      await markNotificationAsRead(notification.id)
    } catch (readError) {
      setActionError(getErrorMessage(readError))
    } finally {
      setBusyNotificationId(null)
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

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-3 text-emerald-950"
      onClick={onClose}
      onPointerDown={event => event.stopPropagation()}
    >
      <div
        className="flex max-h-[88vh] w-full max-w-xl flex-col overflow-hidden rounded-2xl bg-emerald-50 shadow-2xl"
        onClick={event => event.stopPropagation()}
      >
        <div className="flex items-center justify-between gap-3 bg-emerald-700 px-4 py-3 text-white">
          <div>
            <h2 className="text-lg font-bold">🔔 Notificações</h2>
            <p className="text-xs text-emerald-100">
              Acompanhe as interações recentes da sua fazenda
            </p>
          </div>
          <div className="flex items-center gap-2">
            {unreadCount > 0 && (
              <button
                type="button"
                onClick={() => void markAllAsRead()}
                disabled={markingAll}
                className="rounded-lg bg-white/15 px-3 py-2 text-xs font-semibold hover:bg-white/25 disabled:opacity-60"
              >
                {markingAll ? 'Atualizando...' : 'Marcar todas como lidas'}
              </button>
            )}
            <button
              type="button"
              onClick={onClose}
              className="rounded-full px-3 py-1 text-xl hover:bg-white/15"
              aria-label="Fechar painel de notificações"
            >
              ×
            </button>
          </div>
        </div>

        {(error || actionError) && (
          <div
            role="alert"
            className="mx-3 mt-3 rounded-lg border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-700"
          >
            {actionError ?? error}
          </div>
        )}

        <div className="min-h-80 flex-1 overflow-y-auto p-3">
          {loading ? (
            <div className="flex h-64 items-center justify-center text-sm text-emerald-700">
              Carregando notificações...
            </div>
          ) : notifications.length === 0 ? (
            <div className="py-14 text-center text-emerald-700">
              <p className="text-4xl">🌾</p>
              <p className="mt-3 font-semibold">Nenhuma notificação por enquanto.</p>
              <p className="mt-1 text-sm">As novidades da fazenda aparecerão aqui.</p>
            </div>
          ) : (
            <div className="space-y-2">
              {notifications.map(notification => {
                const isUnread = notification.readAt == null
                const isBusy = busyNotificationId === notification.id
                const careFriend = notification.type === NotificationType.CropCaredFor
                  ? friends.find(friend => friend.userId === notification.actorUserId)
                  : undefined

                return (
                  <article
                    key={notification.id}
                    className={`relative flex gap-3 rounded-xl border p-3 shadow-sm transition ${
                      isUnread
                        ? 'border-amber-300 bg-amber-50'
                        : 'border-transparent bg-white/80'
                    }`}
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
                          <h3 className="font-semibold">
                            {getNotificationTitle(notification)}
                          </h3>
                          <p className="mt-0.5 text-sm text-emerald-900">
                            {getNotificationMessage(notification)}
                          </p>
                        </div>
                        {isUnread && (
                          <span
                            className="mt-1 h-2.5 w-2.5 shrink-0 rounded-full bg-red-500"
                            aria-label="Não lida"
                          />
                        )}
                      </div>
                      <div className="mt-2 flex items-center justify-between gap-3">
                        <time
                          dateTime={notification.createdAt}
                          className="text-xs text-emerald-600"
                        >
                          {formatNotificationDate(notification.createdAt)}
                        </time>
                        <div className="flex items-center gap-2">
                          {careFriend && (
                            <button
                              type="button"
                              onClick={() => {
                                if (isUnread) {
                                  void markAsRead(notification)
                                }
                                visitFarm(
                                  careFriend.farmId,
                                  careFriend.userId,
                                  careFriend.username,
                                )
                                onClose()
                              }}
                              className="rounded-lg bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-800 hover:bg-emerald-200"
                            >
                              Visitar {careFriend.username}
                            </button>
                          )}
                          {isUnread && (
                            <button
                              type="button"
                              onClick={() => void markAsRead(notification)}
                              disabled={isBusy}
                              className="rounded-lg px-2 py-1 text-xs font-semibold text-emerald-700 hover:bg-emerald-100 disabled:opacity-60"
                            >
                              {isBusy ? 'Atualizando...' : 'Marcar como lida'}
                            </button>
                          )}
                        </div>
                      </div>
                    </div>
                  </article>
                )
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
