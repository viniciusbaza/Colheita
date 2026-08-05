import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useFarm } from '../farm/useFarmContext'
import { useInventory } from '../inventory/useInventory'
import { useSocial } from '../social/useSocial'
import { NotificationType } from '../types/Social'
import { useUser } from '../user/useUser'
import { getNotificationMessage } from '../utils/notifications'
import {
  FriendsPanel,
  type FriendsPanelTab,
} from './FriendsPanel'
import { InventoryPanel } from './InventoryPanel'
import { NotificationsPanel } from './NotificationsPanel'
import { ShopPanel } from '../shop/ShopPanel'
import type { FarmCameraMode } from '../game/farmCamera'

type PlayerHUDProps = {
  cameraMode: FarmCameraMode
  onCameraModeChange: (mode: FarmCameraMode) => void
}

function dispatchModalState(source: string, open: boolean) {
  window.dispatchEvent(
    new CustomEvent('ui:modal', {
      detail: { source, open },
    }),
  )
}

export function PlayerHUD({
  cameraMode,
  onCameraModeChange,
}: PlayerHUDProps) {
  const { user } = useUser()
  const { farm, isVisiting, visitFarm, returnToOwnFarm, session } = useFarm()
  const { logout } = useAuth()
  const navigate = useNavigate()
  const { inventory } = useInventory()
  const {
    incomingRequests,
    unreadCount,
    toastNotification,
    dismissToast,
    refreshSocial,
    markNotificationAsRead,
  } = useSocial()
  const [showFriends, setShowFriends] = useState(false)
  const [showNotifications, setShowNotifications] = useState(false)
  const [showShop, setShowShop] = useState(false)
  const [showInventory, setShowInventory] = useState(false)
  const [showCameraHint, setShowCameraHint] = useState(false)
  const [friendsInitialTab, setFriendsInitialTab] =
    useState<FriendsPanelTab>('friends')

  const coins = inventory?.coins ?? 0
  const premiumCoins = inventory?.premiumCoins ?? 0

  useEffect(() => {
    if (!toastNotification) return

    const timeoutId = window.setTimeout(dismissToast, 6_000)
    return () => window.clearTimeout(timeoutId)
  }, [dismissToast, toastNotification])

  useEffect(() => {
    if (!showCameraHint) return

    const timeoutId = window.setTimeout(() => {
      setShowCameraHint(false)
    }, 4_000)

    return () => window.clearTimeout(timeoutId)
  }, [showCameraHint])

  function handleLogout() {
    logout()
    navigate('/login')
  }

  function openFriendsPanel(tab: FriendsPanelTab = 'friends') {
    dismissToast()
    setFriendsInitialTab(tab)
    setShowFriends(true)
    dispatchModalState('friends', true)
  }

  function closeFriendsPanel() {
    setShowFriends(false)
    dispatchModalState('friends', false)
  }

  function openNotificationsPanel() {
    dismissToast()
    setShowNotifications(true)
    dispatchModalState('notifications', true)
    void refreshSocial().catch(() => undefined)
  }

  function closeNotificationsPanel() {
    setShowNotifications(false)
    dispatchModalState('notifications', false)
  }

  function openInventoryPanel() {
    setShowInventory(true)
    dispatchModalState('inventory', true)
  }

  function closeInventoryPanel() {
    setShowInventory(false)
    dispatchModalState('inventory', false)
  }

  function openShopPanel() {
    setShowShop(true)
    dispatchModalState('shop', true)
  }

  function closeShopPanel() {
    setShowShop(false)
    dispatchModalState('shop', false)
  }

  function openToastNotification() {
    if (!toastNotification) return

    void markNotificationAsRead(toastNotification.id).catch(() => undefined)

    if (toastNotification.type === NotificationType.FriendRequestReceived) {
      openFriendsPanel('requests')
      return
    }

    openNotificationsPanel()
  }

  function toggleCameraMode() {
    const nextMode = cameraMode === 'focus' ? 'overview' : 'focus'
    setShowCameraHint(nextMode === 'overview')
    onCameraModeChange(nextMode)
  }

  if (!user) return null

  const xpPercent = (user.currentXp / user.xpToNextLevel) * 100
  const isOverview = cameraMode === 'overview'

  return (
    <>
      <div className="pointer-events-auto fixed inset-x-0 top-0 z-50 flex items-center justify-between bg-green-900/[0.99] p-4">
      <div className="flex items-center gap-4 sm:gap-8">
        <div className="hidden sm:block">
          <p className="text-sm">🌾 Fazenda: {farm?.name}</p>
          <p className="font-bold">👤 {user.username}</p>
          <div className="flex items-center gap-4">
            <span className="text-sm">🪙 {coins}</span>
            <span className="text-sm text-pink-300">💎 {premiumCoins}</span>
          </div>
        </div>

        <div>
          <p className="text-sm">⭐ Level {user.level}</p>
          <div className="mt-1 h-2 w-32 overflow-hidden rounded bg-green-700 sm:w-48">
            <div
              className="h-2 rounded bg-yellow-400 transition-all duration-500 ease-out"
              style={{ width: `${xpPercent}%` }}
            />
          </div>
          <small className="opacity-80">
            {user.currentXp}/{user.xpToNextLevel} XP
          </small>
        </div>
      </div>

      {isVisiting && (
        <div className="fixed left-1/2 top-24 z-40 flex -translate-x-1/2 items-center gap-3 rounded-full border border-amber-300 bg-amber-100 px-3 py-1 shadow">
          <span className="text-sm text-amber-900">
            👀 Visitando {farm?.name} de <strong>{session.ownerUsername}</strong>
          </span>
          <button
            type="button"
            onClick={returnToOwnFarm}
            className="rounded-full bg-amber-500 px-3 py-1 text-sm text-white transition hover:bg-amber-600"
          >
            ⬅ Voltar
          </button>
        </div>
      )}

      {toastNotification && (
        <div
          role="status"
          aria-live="polite"
          className="fixed right-4 top-24 z-[60] flex max-w-sm items-start gap-2 rounded-xl border border-emerald-300 bg-white p-3 text-emerald-950 shadow-xl"
        >
          <button type="button" onClick={openToastNotification} className="flex-1 text-left">
            <span className="block text-xs font-bold uppercase tracking-wide text-emerald-600">
              🔔 Nova interação
            </span>
            <span className="mt-1 block text-sm">
              {getNotificationMessage(toastNotification)}
            </span>
          </button>
          <button
            type="button"
            onClick={dismissToast}
            className="rounded px-2 text-lg text-emerald-600 hover:bg-emerald-50"
            aria-label="Fechar notificação"
          >
            ×
          </button>
        </div>
      )}

      <div className="fixed right-20 top-7 z-40 flex gap-1">
        <button
          type="button"
          onClick={openNotificationsPanel}
          className="relative rounded-full bg-emerald-600 px-4 py-1 text-white shadow hover:bg-emerald-700"
          aria-label={`Abrir notificações${unreadCount > 0 ? `, ${unreadCount} não lidas` : ''}`}
        >
          🔔
          {unreadCount > 0 && (
            <span className="absolute -right-2 -top-2 flex min-h-5 min-w-5 items-center justify-center rounded-full bg-red-500 px-1 text-[11px] font-bold text-white ring-2 ring-green-900">
              {unreadCount > 99 ? '99+' : unreadCount}
            </span>
          )}
        </button>

        <button
          type="button"
          onClick={() => openFriendsPanel('friends')}
          className="relative rounded-full bg-emerald-600 px-4 py-1 text-white shadow hover:bg-emerald-700"
          aria-label="Abrir amigos"
        >
          👥
          {incomingRequests.length > 0 && (
            <span className="absolute -right-2 -top-2 flex min-h-5 min-w-5 items-center justify-center rounded-full bg-red-500 px-1 text-[11px] font-bold text-white ring-2 ring-green-900">
              {incomingRequests.length > 99 ? '99+' : incomingRequests.length}
            </span>
          )}
        </button>

        <button
          type="button"
          onClick={openInventoryPanel}
          className="rounded-full bg-emerald-600 px-4 py-1 text-white shadow hover:bg-amber-700"
          aria-label="Abrir inventário"
        >
          🎒
        </button>

        <button
          type="button"
          onClick={openShopPanel}
          className="rounded-full bg-emerald-600 px-4 py-1 text-white shadow hover:bg-emerald-700"
          aria-label="Abrir loja"
        >
          🏪
        </button>
      </div>

      {showFriends && (
        <FriendsPanel
          initialTab={friendsInitialTab}
          onClose={closeFriendsPanel}
          onVisitFriend={friend => {
            visitFarm(friend.farmId, friend.userId, friend.username)
          }}
        />
      )}

      {showNotifications && (
        <NotificationsPanel onClose={closeNotificationsPanel} />
      )}

      {showInventory && <InventoryPanel onClose={closeInventoryPanel} />}
      {showShop && <ShopPanel onClose={closeShopPanel} />}

      <button
        type="button"
        onClick={handleLogout}
        className="rounded-full bg-red-500 px-3 py-1 text-sm text-white hover:bg-red-600"
      >
        Sair
      </button>
      </div>

      <div
        className="pointer-events-none fixed z-30 flex flex-col items-end gap-2"
        style={{
          right: 'max(1rem, env(safe-area-inset-right))',
          bottom: 'max(1rem, env(safe-area-inset-bottom))',
        }}
      >
        {isOverview && showCameraHint && (
          <p
            role="status"
            aria-live="polite"
            className="max-w-52 rounded-xl border border-emerald-800/20 bg-amber-50/95 px-3 py-2 text-right text-xs font-semibold text-emerald-950 shadow-lg backdrop-blur-sm"
          >
            Arraste para mover a fazenda
          </p>
        )}

        <button
          type="button"
          aria-controls="game-container"
          aria-pressed={isOverview}
          aria-label={
            isOverview
              ? 'Centralizar e travar a câmera nos plots'
              : 'Abrir a visão geral e permitir mover a câmera'
          }
          onClick={toggleCameraMode}
          className={`pointer-events-auto min-h-12 rounded-full border-2 px-4 py-2 text-sm font-bold shadow-xl transition focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300 ${
            isOverview
              ? 'border-emerald-950 bg-emerald-700 text-white hover:bg-emerald-600'
              : 'border-amber-700 bg-amber-100 text-amber-950 hover:bg-amber-200'
          }`}
        >
          {isOverview ? 'Centralizar' : 'Visão geral'}
        </button>
      </div>
    </>
  )
}
