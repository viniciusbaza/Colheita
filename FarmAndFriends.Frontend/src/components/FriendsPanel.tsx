import { useEffect, useRef, useState } from 'react'
import { useSocial } from '../social/useSocial'
import type { Friend } from '../types/Friend'
import type { UserSearchResult } from '../types/Social'
import { PlayerAvatar } from './PlayerAvatar'

export type FriendsPanelTab = 'friends' | 'requests' | 'search'

type FriendsPanelProps = {
  initialTab?: FriendsPanelTab
  onClose: () => void
  onVisitFriend: (friend: Friend) => void
}

type Feedback = {
  type: 'success' | 'error'
  message: string
}

const tabClass = (active: boolean) =>
  `relative flex-1 rounded-lg px-2 py-2 text-sm font-semibold transition disabled:cursor-wait disabled:opacity-60 ${
    active
      ? 'bg-emerald-600 text-white shadow-sm'
      : 'text-emerald-700 hover:bg-emerald-100'
  }`

function formatRequestDate(value: string) {
  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Não foi possível concluir a ação.'
}

export function FriendsPanel({
  initialTab = 'friends',
  onClose,
  onVisitFriend,
}: FriendsPanelProps) {
  const {
    friends,
    incomingRequests,
    outgoingRequests,
    loading,
    error,
    searchUsers,
    sendRequest,
    acceptRequest,
    declineRequest,
    cancelRequest,
    removeFriend,
  } = useSocial()
  const [tab, setTab] = useState<FriendsPanelTab>(initialTab)
  const [searchQuery, setSearchQuery] = useState('')
  const [searchResults, setSearchResults] = useState<UserSearchResult[]>([])
  const [searching, setSearching] = useState(false)
  const [searchError, setSearchError] = useState<string | null>(null)
  const [busyAction, setBusyAction] = useState<string | null>(null)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [pendingRemovalUserId, setPendingRemovalUserId] = useState<string | null>(null)
  const [removingUserId, setRemovingUserId] = useState<string | null>(null)
  const removalInFlightRef = useRef<string | null>(null)
  const cancelRemovalButtonRef = useRef<HTMLButtonElement>(null)
  const removalTriggerButtonRef = useRef<HTMLButtonElement | null>(null)

  useEffect(() => {
    if (removalInFlightRef.current !== null) return

    setPendingRemovalUserId(null)
    setTab(initialTab)
  }, [initialTab])

  useEffect(() => {
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key !== 'Escape') return

      if (pendingRemovalUserId) {
        if (removalInFlightRef.current === null) setPendingRemovalUserId(null)
        return
      }

      onClose()
    }

    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [onClose, pendingRemovalUserId])

  useEffect(() => {
    if (pendingRemovalUserId) {
      cancelRemovalButtonRef.current?.focus()
      return
    }

    removalTriggerButtonRef.current?.focus()
    removalTriggerButtonRef.current = null
  }, [pendingRemovalUserId])

  useEffect(() => {
    if (!feedback) return

    const timeoutId = window.setTimeout(() => setFeedback(null), 3_500)
    return () => window.clearTimeout(timeoutId)
  }, [feedback])

  useEffect(() => {
    const query = searchQuery.trim()

    if (query.length < 2) {
      setSearchResults([])
      setSearchError(null)
      setSearching(false)
      return
    }

    let active = true
    const timeoutId = window.setTimeout(async () => {
      setSearching(true)
      setSearchError(null)

      try {
        const results = await searchUsers(query)
        if (active) setSearchResults(results)
      } catch (searchRequestError) {
        if (active) {
          setSearchResults([])
          setSearchError(getErrorMessage(searchRequestError))
        }
      } finally {
        if (active) setSearching(false)
      }
    }, 350)

    return () => {
      active = false
      window.clearTimeout(timeoutId)
    }
  }, [searchQuery, searchUsers])

  async function runAction(
    actionKey: string,
    action: () => Promise<void>,
    successMessage: string,
  ) {
    setBusyAction(actionKey)
    setFeedback(null)

    try {
      await action()
      setFeedback({ type: 'success', message: successMessage })
    } catch (actionError) {
      setFeedback({ type: 'error', message: getErrorMessage(actionError) })
    } finally {
      setBusyAction(null)
    }
  }

  function visitFriend(friend: Friend) {
    if (removalInFlightRef.current !== null) return

    onVisitFriend(friend)
    onClose()
  }

  function changeTab(nextTab: FriendsPanelTab) {
    if (removalInFlightRef.current !== null) return

    setPendingRemovalUserId(null)
    setTab(nextTab)
  }

  function closePanel() {
    if (removalInFlightRef.current !== null) return
    onClose()
  }

  function requestFriendRemoval(friend: Friend, triggerButton: HTMLButtonElement) {
    if (removalInFlightRef.current !== null) return

    removalTriggerButtonRef.current = triggerButton
    setFeedback(null)
    setPendingRemovalUserId(friend.userId)
  }

  function cancelFriendRemoval() {
    if (removalInFlightRef.current !== null) return
    setPendingRemovalUserId(null)
  }

  async function confirmFriendRemoval(friend: Friend) {
    if (removalInFlightRef.current !== null) return

    removalInFlightRef.current = friend.userId
    setRemovingUserId(friend.userId)
    setFeedback(null)

    try {
      await removeFriend(friend.userId)
      setPendingRemovalUserId(null)
      setFeedback({
        type: 'success',
        message: `${friend.username} foi removido da sua lista.`,
      })
    } catch (actionError) {
      setFeedback({ type: 'error', message: getErrorMessage(actionError) })
    } finally {
      removalInFlightRef.current = null
      setRemovingUserId(null)
    }
  }

  const isFriendRemovalInFlight = removingUserId !== null

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-3 text-emerald-950"
      onClick={closePanel}
      onPointerDown={event => event.stopPropagation()}
    >
      <div
        className="flex max-h-[88vh] w-full max-w-xl flex-col overflow-hidden rounded-2xl bg-emerald-50 shadow-2xl"
        onClick={event => event.stopPropagation()}
      >
        <div className="flex items-center justify-between bg-emerald-700 px-4 py-3 text-white">
          <div>
            <h2 className="text-lg font-bold">👥 Amigos</h2>
            <p className="text-xs text-emerald-100">Visite fazendas e jogue com seus amigos</p>
          </div>
          <button
            type="button"
            onClick={closePanel}
            disabled={isFriendRemovalInFlight}
            className="rounded-full px-3 py-1 text-xl hover:bg-white/15 disabled:cursor-wait disabled:opacity-50"
            aria-label="Fechar painel de amigos"
          >
            ×
          </button>
        </div>

        <div className="flex gap-1 border-b border-emerald-200 bg-white/70 p-2">
          <button
            type="button"
            className={tabClass(tab === 'friends')}
            onClick={() => changeTab('friends')}
            disabled={isFriendRemovalInFlight}
          >
            Amigos
          </button>
          <button
            type="button"
            className={tabClass(tab === 'requests')}
            onClick={() => changeTab('requests')}
            disabled={isFriendRemovalInFlight}
          >
            Solicitações
            {incomingRequests.length > 0 && (
              <span className="ml-1 inline-flex min-w-5 justify-center rounded-full bg-amber-400 px-1 text-xs text-amber-950">
                {incomingRequests.length}
              </span>
            )}
          </button>
          <button
            type="button"
            className={tabClass(tab === 'search')}
            onClick={() => changeTab('search')}
            disabled={isFriendRemovalInFlight}
          >
            Adicionar
          </button>
        </div>

        {feedback && (
          <div
            role={feedback.type === 'error' ? 'alert' : 'status'}
            aria-live={feedback.type === 'error' ? 'assertive' : 'polite'}
            className={`mx-3 mt-3 rounded-lg border px-3 py-2 text-sm ${
              feedback.type === 'success'
                ? 'border-emerald-300 bg-emerald-100 text-emerald-800'
                : 'border-red-300 bg-red-50 text-red-700'
            }`}
          >
            {feedback.message}
          </div>
        )}

        {error && (
          <div className="mx-3 mt-3 rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-800">
            {error}
          </div>
        )}

        <div className="min-h-80 flex-1 overflow-y-auto p-3">
          {loading ? (
            <div className="flex h-64 items-center justify-center text-sm text-emerald-700">
              Carregando dados sociais...
            </div>
          ) : tab === 'friends' ? (
            <div className="space-y-2">
              {friends.length === 0 ? (
                <div className="py-12 text-center text-sm text-emerald-700">
                  <p className="text-3xl">👥</p>
                  <p className="mt-2 font-semibold">Sua lista ainda está vazia.</p>
                  <button
                    type="button"
                    onClick={() => changeTab('search')}
                    className="mt-3 rounded-lg bg-emerald-600 px-4 py-2 text-white hover:bg-emerald-700"
                  >
                    Encontrar amigos
                  </button>
                </div>
              ) : (
                friends.map(friend => {
                  const isConfirmingRemoval = pendingRemovalUserId === friend.userId
                  const isRemoving = removingUserId === friend.userId

                  return (
                    <div
                      key={friend.userId}
                      className="rounded-xl bg-white p-3 shadow-sm"
                    >
                      <div className="flex flex-wrap items-center gap-3 sm:flex-nowrap">
                        <PlayerAvatar
                          avatarId={friend.avatarId}
                          alt={`Avatar de ${friend.username}`}
                        />
                        <div className="min-w-0 flex-1">
                          <p className="truncate font-semibold">{friend.username}</p>
                          <p className="truncate text-sm text-emerald-600">{friend.farmName}</p>
                        </div>
                        <div className="flex w-full justify-end gap-2 sm:w-auto">
                          <button
                            type="button"
                            onClick={() => visitFriend(friend)}
                            disabled={isConfirmingRemoval || isFriendRemovalInFlight}
                            className="min-h-11 rounded-lg bg-emerald-600 px-3 py-2 text-sm font-semibold text-white hover:bg-emerald-700 disabled:cursor-wait disabled:opacity-50"
                          >
                            Visitar
                          </button>
                          <button
                            type="button"
                            onClick={event => requestFriendRemoval(friend, event.currentTarget)}
                            disabled={isConfirmingRemoval || isFriendRemovalInFlight}
                            className="min-h-11 rounded-lg px-2 py-2 text-sm text-red-600 hover:bg-red-50 disabled:cursor-wait disabled:opacity-50"
                            aria-label={`Remover ${friend.username}`}
                            aria-expanded={isConfirmingRemoval}
                            aria-controls={isConfirmingRemoval
                              ? `remove-friend-confirmation-${friend.userId}`
                              : undefined}
                          >
                            Remover
                          </button>
                        </div>
                      </div>

                      {isConfirmingRemoval && (
                        <div
                          id={`remove-friend-confirmation-${friend.userId}`}
                          role="group"
                          aria-busy={isRemoving}
                          aria-labelledby={`remove-friend-title-${friend.userId}`}
                          aria-describedby={`remove-friend-description-${friend.userId}`}
                          className="mt-3 rounded-lg border border-red-200 bg-red-50 p-3"
                        >
                          <p
                            id={`remove-friend-title-${friend.userId}`}
                            className="text-sm font-bold text-red-800"
                          >
                            Remover amizade?
                          </p>
                          <p
                            id={`remove-friend-description-${friend.userId}`}
                            className="mt-1 text-sm text-red-700"
                          >
                            Deseja remover {friend.username} da sua lista de amigos?
                          </p>
                          <div className="mt-3 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                            <button
                              ref={cancelRemovalButtonRef}
                              type="button"
                              onClick={cancelFriendRemoval}
                              disabled={isRemoving}
                              aria-describedby={`remove-friend-description-${friend.userId}`}
                              className="min-h-11 rounded-lg border border-emerald-300 bg-white px-3 py-2 text-sm font-semibold text-emerald-800 hover:bg-emerald-100 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300 disabled:cursor-wait disabled:opacity-50"
                            >
                              Cancelar
                            </button>
                            <button
                              type="button"
                              onClick={() => void confirmFriendRemoval(friend)}
                              disabled={isRemoving}
                              aria-describedby={`remove-friend-description-${friend.userId}`}
                              className="min-h-11 rounded-lg border border-red-700 bg-red-600 px-3 py-2 text-sm font-semibold text-white hover:bg-red-500 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300 disabled:cursor-wait disabled:opacity-50"
                            >
                              {isRemoving ? 'Removendo...' : 'Sim, remover'}
                            </button>
                          </div>
                        </div>
                      )}
                    </div>
                  )
                })
              )}
            </div>
          ) : tab === 'requests' ? (
            <div className="space-y-5">
              <section>
                <h3 className="mb-2 text-sm font-bold uppercase tracking-wide text-emerald-700">
                  Recebidas ({incomingRequests.length})
                </h3>
                {incomingRequests.length === 0 ? (
                  <p className="rounded-xl bg-white/70 p-4 text-center text-sm text-emerald-600">
                    Nenhuma solicitação recebida.
                  </p>
                ) : (
                  <div className="space-y-2">
                    {incomingRequests.map(request => (
                      <div key={request.id} className="rounded-xl bg-white p-3 shadow-sm">
                        <div className="flex items-center justify-between gap-3">
                          <div>
                            <p className="font-semibold">{request.requesterUsername}</p>
                            <p className="text-xs text-emerald-600">
                              Enviado em {formatRequestDate(request.createdAt)}
                            </p>
                          </div>
                          <div className="flex gap-2">
                            <button
                              type="button"
                              disabled={busyAction === request.id}
                              onClick={() => void runAction(
                                request.id,
                                () => acceptRequest(request.id),
                                `Agora você e ${request.requesterUsername} são amigos.`,
                              )}
                              className="rounded-lg bg-emerald-600 px-3 py-2 text-sm font-semibold text-white hover:bg-emerald-700 disabled:opacity-50"
                            >
                              Aceitar
                            </button>
                            <button
                              type="button"
                              disabled={busyAction === request.id}
                              onClick={() => void runAction(
                                request.id,
                                () => declineRequest(request.id),
                                'Solicitação recusada.',
                              )}
                              className="rounded-lg border border-red-200 px-3 py-2 text-sm text-red-600 hover:bg-red-50 disabled:opacity-50"
                            >
                              Recusar
                            </button>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </section>

              <section>
                <h3 className="mb-2 text-sm font-bold uppercase tracking-wide text-emerald-700">
                  Enviadas ({outgoingRequests.length})
                </h3>
                {outgoingRequests.length === 0 ? (
                  <p className="rounded-xl bg-white/70 p-4 text-center text-sm text-emerald-600">
                    Nenhuma solicitação aguardando resposta.
                  </p>
                ) : (
                  <div className="space-y-2">
                    {outgoingRequests.map(request => (
                      <div
                        key={request.id}
                        className="flex items-center justify-between gap-3 rounded-xl bg-white p-3 shadow-sm"
                      >
                        <div>
                          <p className="font-semibold">{request.recipientUsername}</p>
                          <p className="text-xs text-emerald-600">Aguardando resposta</p>
                        </div>
                        <button
                          type="button"
                          disabled={busyAction === request.id}
                          onClick={() => void runAction(
                            request.id,
                            () => cancelRequest(request.id),
                            'Solicitação cancelada.',
                          )}
                          className="rounded-lg border border-amber-300 px-3 py-2 text-sm text-amber-700 hover:bg-amber-50 disabled:opacity-50"
                        >
                          Cancelar
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </section>
            </div>
          ) : (
            <div>
              <label htmlFor="friend-search" className="text-sm font-semibold text-emerald-800">
                Buscar por nome de usuário
              </label>
              <input
                id="friend-search"
                type="search"
                value={searchQuery}
                onChange={event => setSearchQuery(event.target.value)}
                placeholder="Digite pelo menos 2 caracteres"
                autoFocus
                className="mt-2 w-full rounded-xl border border-emerald-300 bg-white px-4 py-3 outline-none transition focus:border-emerald-600 focus:ring-2 focus:ring-emerald-200"
              />

              <div className="mt-3 space-y-2">
                {searching ? (
                  <p className="py-8 text-center text-sm text-emerald-600">Buscando jogadores...</p>
                ) : searchError ? (
                  <p className="rounded-lg bg-red-50 p-3 text-sm text-red-700">{searchError}</p>
                ) : searchQuery.trim().length < 2 ? (
                  <p className="py-8 text-center text-sm text-emerald-600">
                    Procure um jogador para enviar uma solicitação.
                  </p>
                ) : searchResults.length === 0 ? (
                  <p className="py-8 text-center text-sm text-emerald-600">
                    Nenhum jogador encontrado.
                  </p>
                ) : (
                  searchResults.map(result => {
                    const isFriend = friends.some(friend => friend.userId === result.id)
                    const incoming = incomingRequests.find(
                      request => request.requesterUserId === result.id,
                    )
                    const outgoing = outgoingRequests.find(
                      request => request.recipientUserId === result.id,
                    )
                    const actionKey = `send-${result.id}`

                    return (
                      <div
                        key={result.id}
                        className="flex items-center gap-3 rounded-xl bg-white p-3 shadow-sm"
                      >
                        <div className="flex h-10 w-10 items-center justify-center rounded-full bg-emerald-200 font-bold text-emerald-800">
                          {result.username.slice(0, 1).toUpperCase()}
                        </div>
                        <p className="min-w-0 flex-1 truncate font-semibold">{result.username}</p>

                        {isFriend ? (
                          <span className="text-sm font-semibold text-emerald-600">Já é amigo</span>
                        ) : incoming ? (
                          <button
                            type="button"
                            onClick={() => changeTab('requests')}
                            className="rounded-lg bg-amber-100 px-3 py-2 text-sm font-semibold text-amber-800"
                          >
                            Responder convite
                          </button>
                        ) : outgoing ? (
                          <span className="text-sm font-semibold text-amber-700">Convite enviado</span>
                        ) : (
                          <button
                            type="button"
                            disabled={busyAction === actionKey}
                            onClick={() => void runAction(
                              actionKey,
                              () => sendRequest(result.id),
                              `Solicitação enviada para ${result.username}.`,
                            )}
                            className="rounded-lg bg-emerald-600 px-3 py-2 text-sm font-semibold text-white hover:bg-emerald-700 disabled:opacity-50"
                          >
                            {busyAction === actionKey ? 'Enviando...' : 'Adicionar'}
                          </button>
                        )}
                      </div>
                    )
                  })
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
