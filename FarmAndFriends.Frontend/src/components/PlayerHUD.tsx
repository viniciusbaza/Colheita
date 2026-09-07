import { useCallback, useEffect, useRef, useState } from 'react'
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
import { PlayerAvatar } from './PlayerAvatar'
import { ProfilePanel } from './ProfilePanel'

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
  const [showLogoutConfirmation, setShowLogoutConfirmation] = useState(false)
  const [showProfile, setShowProfile] = useState(false)
  const profileTriggerRef = useRef<HTMLButtonElement | null>(null)
  const [friendsInitialTab, setFriendsInitialTab] =
    useState<FriendsPanelTab>('friends')
  const logoutTriggerRef = useRef<HTMLButtonElement | null>(null)
  const logoutDialogRef = useRef<HTMLDivElement | null>(null)
  const cancelLogoutButtonRef = useRef<HTMLButtonElement | null>(null)
  const logoutModalOpenRef = useRef(false)

  const coins = inventory?.coins ?? 0
  const premiumCoins = inventory?.premiumCoins ?? 0

  const closeProfile = useCallback(() => setShowProfile(false), [])

  function openProfile(trigger: HTMLButtonElement) {
    profileTriggerRef.current = trigger
    dismissToast()
    dispatchModalState('profile', true)
    setShowProfile(true)
  }

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

  function openLogoutConfirmation(trigger: HTMLButtonElement) {
    logoutTriggerRef.current = trigger
    logoutModalOpenRef.current = true
    setShowLogoutConfirmation(true)
    dispatchModalState('logout-confirmation', true)
  }

  const closeLogoutConfirmation = useCallback((restoreFocus = true) => {
    logoutModalOpenRef.current = false
    setShowLogoutConfirmation(false)
    dispatchModalState('logout-confirmation', false)

    if (restoreFocus) {
      window.requestAnimationFrame(() => logoutTriggerRef.current?.focus())
    }
  }, [])

  function confirmLogout() {
    closeLogoutConfirmation(false)
    logout()
    navigate('/login')
  }

  useEffect(() => {
    if (!showLogoutConfirmation) return

    cancelLogoutButtonRef.current?.focus()

    function handleDialogKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault()
        closeLogoutConfirmation()
        return
      }

      if (event.key !== 'Tab') return

      const focusableElements = logoutDialogRef.current?.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [href], [tabindex]:not([tabindex="-1"])',
      )
      if (!focusableElements?.length) return

      const firstElement = focusableElements[0]
      const lastElement = focusableElements[focusableElements.length - 1]

      if (event.shiftKey && document.activeElement === firstElement) {
        event.preventDefault()
        lastElement.focus()
      } else if (!event.shiftKey && document.activeElement === lastElement) {
        event.preventDefault()
        firstElement.focus()
      }
    }

    window.addEventListener('keydown', handleDialogKeyDown)
    return () => window.removeEventListener('keydown', handleDialogKeyDown)
  }, [closeLogoutConfirmation, showLogoutConfirmation])

  useEffect(() => () => {
    if (logoutModalOpenRef.current) {
      dispatchModalState('logout-confirmation', false)
    }
  }, [])

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

  function openFriendRequestsFromNotifications() {
    setShowNotifications(false)
    dispatchModalState('notifications', false)
    openFriendsPanel('requests')
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

  const xpPercent = user.xpToNextLevel > 0
    ? Math.min(100, Math.max(0, (user.currentXp / user.xpToNextLevel) * 100))
    : 0
  const isOverview = cameraMode === 'overview'

  return (
    <>
      <div
        className="pointer-events-none fixed inset-x-0 top-0 z-50 flex items-start justify-between gap-2 px-2 pb-2 sm:gap-4 md:px-4"
        style={{ paddingTop: 'max(0.5rem, env(safe-area-inset-top))' }}
      >
        <section
          aria-label="Status do jogador"
          className="relative flex min-h-16 w-[6.125rem] shrink-0 items-center gap-1 rounded-2xl border border-emerald-100/40 bg-emerald-950/85 px-0.5 pb-2 pt-1 text-white shadow-xl ring-1 ring-black/10 backdrop-blur-md sm:w-auto sm:min-w-80 sm:gap-3 sm:px-3 sm:py-2 md:min-w-[22rem] md:px-4"
        >
          <button
            type="button"
            onClick={event => openProfile(event.currentTarget)}
            onPointerDown={event => event.stopPropagation()}
            aria-label={`Editar perfil de ${user.username}`}
            aria-haspopup="dialog"
            title="Meu perfil"
            className="pointer-events-auto relative flex h-11 w-11 shrink-0 items-center justify-center rounded-full focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300"
          >
            <PlayerAvatar avatarId={user.avatarId} />
            <span className="absolute -bottom-1 -right-1 rounded-full border border-emerald-200 bg-emerald-950 px-1 text-[10px] font-bold sm:hidden" aria-hidden="true">⭐{user.level}</span>
          </button>
          <div
            role="progressbar"
            aria-label={`Nível ${user.level}, progresso de experiência`}
            aria-valuemin={0}
            aria-valuemax={user.xpToNextLevel}
            aria-valuenow={Math.min(user.currentXp, user.xpToNextLevel)}
            className="absolute bottom-1 left-1 right-1 h-1 overflow-hidden rounded-full bg-emerald-800 sm:hidden"
          >
            <div className="h-full rounded-full bg-yellow-300" style={{ width: `${xpPercent}%` }} />
          </div>
          <div className="hidden min-w-0 md:block">
            <p className="max-w-32 truncate text-xs text-emerald-100 lg:max-w-48">
              🌾 {user.farmName}
            </p>
            <p className="max-w-32 truncate text-sm font-bold lg:max-w-48">👤 {user.username}</p>
            <div className="flex items-center gap-3 text-xs font-semibold">
              <span>🪙 {coins}</span>
              <span className="text-pink-200">💵 {premiumCoins}</span>
            </div>
          </div>

          <div className="hidden min-w-0 flex-1 sm:block md:border-l md:border-white/20 md:pl-4">
            <div className="flex items-center justify-between gap-2 text-xs font-semibold">
              <span className="whitespace-nowrap sm:hidden">⭐ {user.level}</span>
              <span className="hidden whitespace-nowrap sm:inline">⭐ Nível {user.level}</span>
              <span className="hidden items-center gap-2 sm:flex md:hidden">
                <span className="whitespace-nowrap">🪙 {coins}</span>
                <span className="whitespace-nowrap text-pink-200">💵 {premiumCoins}</span>
              </span>
            </div>
            <div
              className="mt-1 h-1.5 w-full overflow-hidden rounded-full bg-emerald-800/90 sm:h-2 sm:min-w-24 md:w-28 lg:w-40"
              role="progressbar"
              aria-label="Progresso de experiência"
              aria-valuemin={0}
              aria-valuemax={user.xpToNextLevel}
              aria-valuenow={Math.min(user.currentXp, user.xpToNextLevel)}
            >
              <div
                className="h-full rounded-full bg-yellow-300 transition-all duration-500 ease-out"
                style={{ width: `${xpPercent}%` }}
              />
            </div>
            <small className="hidden truncate text-[10px] text-emerald-100 sm:block md:text-xs">
              {user.currentXp}/{user.xpToNextLevel} XP
            </small>
          </div>

          <button
            type="button"
            onClick={event => openLogoutConfirmation(event.currentTarget)}
            className="pointer-events-auto inline-flex min-h-11 min-w-11 shrink-0 items-center justify-center rounded-xl border border-red-200/60 bg-red-600/90 px-2 text-sm font-bold text-white shadow-md transition hover:bg-red-500 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300 md:hidden"
            aria-label="Sair do jogo"
            title="Sair"
          >
            🚪
          </button>
        </section>

        <nav
          aria-label="Atalhos do jogo"
          className="ml-auto flex items-center gap-1 rounded-2xl border border-emerald-100/40 bg-emerald-950/85 p-1 text-white shadow-xl ring-1 ring-black/10 backdrop-blur-md sm:gap-1.5 sm:p-1.5"
        >
          <button
            type="button"
            onClick={openNotificationsPanel}
            className="pointer-events-auto relative inline-flex min-h-11 min-w-11 items-center justify-center gap-2 rounded-xl border border-emerald-100/40 bg-emerald-800/90 px-2 text-sm font-bold text-white shadow-md transition hover:bg-emerald-700 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300 active:translate-y-px sm:px-3"
            aria-label={`Abrir acontecimentos${unreadCount > 0 ? `, ${unreadCount} não vistos` : ''}`}
            title="Acontecimentos"
          >
            <span aria-hidden="true">🔔</span>
            <span className="hidden xl:inline">Acontecimentos</span>
            {unreadCount > 0 && (
              <span className="absolute -right-1.5 -top-1.5 flex min-h-5 min-w-5 items-center justify-center rounded-full bg-red-500 px-1 text-[11px] font-bold text-white ring-2 ring-emerald-950">
                {unreadCount > 99 ? '99+' : unreadCount}
              </span>
            )}
          </button>

          <button
            type="button"
            onClick={() => openFriendsPanel('friends')}
            className="pointer-events-auto relative inline-flex min-h-11 min-w-11 items-center justify-center gap-2 rounded-xl border border-emerald-100/40 bg-emerald-800/90 px-2 text-sm font-bold text-white shadow-md transition hover:bg-emerald-700 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300 active:translate-y-px sm:px-3"
            aria-label={`Abrir amigos${incomingRequests.length > 0 ? `, ${incomingRequests.length} solicitações pendentes` : ''}`}
            title="Amigos"
          >
            <span aria-hidden="true">👥</span>
            <span className="hidden xl:inline">Amigos</span>
            {incomingRequests.length > 0 && (
              <span className="absolute -right-1.5 -top-1.5 flex min-h-5 min-w-5 items-center justify-center rounded-full bg-red-500 px-1 text-[11px] font-bold text-white ring-2 ring-emerald-950">
                {incomingRequests.length > 99 ? '99+' : incomingRequests.length}
              </span>
            )}
          </button>

          <button
            type="button"
            onClick={openInventoryPanel}
            className="pointer-events-auto inline-flex min-h-11 min-w-11 items-center justify-center gap-2 rounded-xl border border-emerald-100/40 bg-emerald-800/90 px-2 text-sm font-bold text-white shadow-md transition hover:bg-emerald-700 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300 active:translate-y-px sm:px-3"
            aria-label="Abrir inventário"
            title="Inventário"
          >
            <span aria-hidden="true">🎒</span>
            <span className="hidden xl:inline">Inventário</span>
          </button>

          <button
            type="button"
            onClick={openShopPanel}
            className="pointer-events-auto inline-flex min-h-11 min-w-11 items-center justify-center gap-2 rounded-xl border border-emerald-100/40 bg-emerald-800/90 px-2 text-sm font-bold text-white shadow-md transition hover:bg-emerald-700 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300 active:translate-y-px sm:px-3"
            aria-label="Abrir loja"
            title="Loja"
          >
            <span aria-hidden="true">🏪</span>
            <span className="hidden xl:inline">Loja</span>
          </button>

          <button
            type="button"
            onClick={event => openLogoutConfirmation(event.currentTarget)}
            className="pointer-events-auto hidden min-h-11 items-center justify-center gap-2 rounded-xl border border-red-200/60 bg-red-600/90 px-3 text-sm font-bold text-white shadow-md transition hover:bg-red-500 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300 active:translate-y-px md:inline-flex"
            aria-label="Sair do jogo"
            title="Sair"
          >
            <span aria-hidden="true">🚪</span>
            <span className="hidden xl:inline">Sair</span>
          </button>
        </nav>
      </div>

      {isVisiting && (
        <div className="pointer-events-none fixed left-2 right-2 top-20 z-40 flex items-center justify-center sm:left-1/2 sm:right-auto sm:top-24 sm:-translate-x-1/2">
          <div className="flex max-w-full items-center gap-2 rounded-2xl border border-amber-300 bg-amber-50/95 px-3 py-2 text-amber-950 shadow-lg backdrop-blur-sm">
            <span className="min-w-0 truncate text-xs sm:text-sm">
              👀 Visitando {farm?.name} de <strong>{session.ownerUsername}</strong>
            </span>
            <button
              type="button"
              onClick={returnToOwnFarm}
              className="pointer-events-auto inline-flex min-h-10 shrink-0 items-center rounded-xl bg-amber-500 px-3 text-xs font-bold text-white transition hover:bg-amber-600 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-emerald-300 sm:text-sm"
            >
              ⬅ Voltar
            </button>
          </div>
        </div>
      )}

      {toastNotification && (
        <div
          role="status"
          aria-live="polite"
          className={`pointer-events-none fixed left-2 right-2 z-[60] flex items-start gap-2 rounded-xl border border-emerald-300 bg-white/95 p-3 text-emerald-950 shadow-xl backdrop-blur-sm sm:left-auto sm:right-4 sm:max-w-sm ${
            isVisiting ? 'top-36 sm:top-36' : 'top-20 sm:top-24'
          }`}
        >
          <button
            type="button"
            onClick={openToastNotification}
            className="pointer-events-auto flex-1 rounded-lg text-left focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-emerald-300"
          >
            <span className="block text-xs font-bold uppercase tracking-wide text-emerald-700">
              🔔 Novo acontecimento
            </span>
            <span className="mt-1 block text-sm">
              {getNotificationMessage(toastNotification)}
            </span>
          </button>
          <button
            type="button"
            onClick={dismissToast}
            className="pointer-events-auto inline-flex min-h-10 min-w-10 items-center justify-center rounded-lg text-lg text-emerald-700 hover:bg-emerald-50 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-emerald-300"
            aria-label="Fechar acontecimento"
          >
            ×
          </button>
        </div>
      )}

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
        <NotificationsPanel
          onClose={closeNotificationsPanel}
          onOpenFriendRequests={openFriendRequestsFromNotifications}
        />
      )}

      {showInventory && <InventoryPanel onClose={closeInventoryPanel} />}
      {showShop && <ShopPanel onClose={closeShopPanel} />}
      {showProfile && <ProfilePanel onClose={closeProfile} triggerRef={profileTriggerRef} />}

      {showLogoutConfirmation && (
        <div
          className="fixed inset-0 z-[80] flex items-center justify-center bg-black/55 p-4 text-emerald-950 backdrop-blur-[2px]"
          onClick={() => closeLogoutConfirmation()}
          onPointerDown={event => event.stopPropagation()}
        >
          <div
            ref={logoutDialogRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby="logout-confirmation-title"
            aria-describedby="logout-confirmation-description"
            className="w-full max-w-sm overflow-hidden rounded-2xl border border-emerald-200 bg-emerald-50 shadow-2xl"
            onClick={event => event.stopPropagation()}
          >
            <header className="flex items-start justify-between gap-3 bg-emerald-800 px-4 py-3 text-white">
              <div>
                <h2 id="logout-confirmation-title" className="text-lg font-bold">
                  Sair do jogo?
                </h2>
                <p className="mt-0.5 text-xs text-emerald-100">
                  Confirme antes de encerrar sua sessão.
                </p>
              </div>
              <button
                type="button"
                onClick={() => closeLogoutConfirmation()}
                className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-xl text-2xl text-emerald-50 transition hover:bg-white/15 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300"
                aria-label="Fechar confirmação e continuar jogando"
              >
                ×
              </button>
            </header>

            <div className="p-4 sm:p-5">
              <p id="logout-confirmation-description" className="text-sm leading-relaxed text-emerald-900">
                Você precisará entrar novamente para voltar à sua fazenda.
              </p>

              <div className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                <button
                  ref={cancelLogoutButtonRef}
                  type="button"
                  onClick={() => closeLogoutConfirmation()}
                  className="inline-flex min-h-11 items-center justify-center rounded-xl border border-emerald-300 bg-white px-4 text-sm font-bold text-emerald-800 transition hover:bg-emerald-100 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300"
                >
                  Continuar jogando
                </button>
                <button
                  type="button"
                  onClick={confirmLogout}
                  className="inline-flex min-h-11 items-center justify-center rounded-xl border border-red-700 bg-red-600 px-4 text-sm font-bold text-white shadow-sm transition hover:bg-red-500 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300"
                >
                  Sair da conta
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

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
          title={isOverview ? 'Centralizar fazenda' : 'Abrir visão geral'}
          onClick={toggleCameraMode}
          className={`pointer-events-auto relative inline-flex min-h-12 min-w-12 items-center justify-center rounded-full border-2 p-2 text-xl shadow-xl transition focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300 ${
            isOverview
              ? 'border-emerald-950 bg-emerald-700 text-white hover:bg-emerald-600'
              : 'border-amber-700 bg-amber-100 text-amber-950 hover:bg-amber-200'
          }`}
        >
          <span aria-hidden="true">🔍</span>
          <span
            aria-hidden="true"
            className={`absolute -right-1 -top-1 inline-flex h-5 min-w-5 items-center justify-center rounded-full border px-1 text-sm font-black leading-none shadow-sm ${
              isOverview
                ? 'border-emerald-100 bg-white text-emerald-900'
                : 'border-amber-100 bg-emerald-900 text-white'
            }`}
          >
            {isOverview ? '+' : '−'}
          </span>
        </button>
      </div>
    </>
  )
}
