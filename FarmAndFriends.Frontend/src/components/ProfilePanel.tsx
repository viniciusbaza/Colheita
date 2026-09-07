import { useEffect, useReducer, useRef, type RefObject } from 'react'
import { useUser } from '../user/useUser'
import { AVATARS, getAvatar } from '../profile/avatarCatalog'
import {
  canSaveProfile,
  createProfileEditor,
  FARM_NAME_MAX_LENGTH,
  getTrimmedFarmName,
  profileEditorReducer,
} from '../profile/profileEditor'
import { PlayerAvatar } from './PlayerAvatar'
import { useFarm } from '../farm/useFarmContext'

export function ProfilePanel({ onClose, triggerRef }: {
  onClose: () => void
  triggerRef: RefObject<HTMLButtonElement | null>
}) {
  const { user, saveProfile } = useUser()
  const { updateOwnFarmName } = useFarm()
  const [state, dispatch] = useReducer(
    profileEditorReducer,
    { avatarId: user?.avatarId, farmName: user?.farmName },
    createProfileEditor,
  )
  const dialogRef = useRef<HTMLDivElement>(null)
  const closeRef = useRef<HTMLButtonElement>(null)
  const savingRef = useRef(false)
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    const trigger = triggerRef.current
    window.dispatchEvent(new CustomEvent('ui:modal', { detail: { source: 'profile', open: true } }))
    closeRef.current?.focus()

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault()
        event.stopPropagation()
        if (!savingRef.current) onClose()
      }
      if (event.key !== 'Tab') return
      const elements = dialogRef.current?.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), [tabindex="0"]',
      )
      if (!elements?.length) {
        event.preventDefault()
        dialogRef.current?.focus()
        return
      }
      const first = elements[0]
      const last = elements[elements.length - 1]
      const inside = dialogRef.current?.contains(document.activeElement)
      if (event.shiftKey && (document.activeElement === first || !inside)) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && (document.activeElement === last || !inside)) {
        event.preventDefault()
        first.focus()
      }
    }
    window.addEventListener('keydown', handleKeyDown, true)
    return () => {
      mounted.current = false
      window.removeEventListener('keydown', handleKeyDown, true)
      window.dispatchEvent(new CustomEvent('ui:modal', { detail: { source: 'profile', open: false } }))
      if (trigger?.isConnected) trigger.focus()
    }
  }, [onClose, triggerRef])

  function close() {
    if (savingRef.current) return
    dispatch({ type: 'cancel' })
    onClose()
  }

  async function save() {
    if (savingRef.current || !canSaveProfile(state)) return
    savingRef.current = true
    dispatch({ type: 'save' })
    dialogRef.current?.focus()
    try {
      const profile = await saveProfile({
        avatarId: state.draftAvatar,
        farmName: getTrimmedFarmName(state),
      })
      if (mounted.current) {
        updateOwnFarmName(profile.farmId, profile.farmName)
        dispatch({
          type: 'saved',
          avatarId: profile.avatarId,
          farmName: profile.farmName,
        })
        onClose()
      }
    } catch {
      if (mounted.current) {
        dispatch({
          type: 'failed',
          message: 'Não foi possível salvar seu perfil. Tente novamente.',
        })
      }
    } finally {
      savingRef.current = false
      window.requestAnimationFrame(() => {
        if (mounted.current) closeRef.current?.focus()
      })
    }
  }

  const trimmedFarmName = getTrimmedFarmName(state)
  const farmNameIsEmpty = trimmedFarmName.length === 0

  return (
    <div
      className="fixed inset-0 z-[80] flex items-center justify-center bg-black/55 p-3 text-emerald-950 backdrop-blur-[2px]"
      onClick={close}
      onPointerDown={event => event.stopPropagation()}
      onWheel={event => event.stopPropagation()}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="profile-title"
        aria-describedby="profile-description"
        aria-busy={state.saving}
        tabIndex={-1}
        className="flex max-h-[90dvh] w-full max-w-md flex-col overflow-hidden rounded-2xl border border-emerald-200 bg-emerald-50 shadow-2xl"
        onClick={event => event.stopPropagation()}
      >
        <header className="flex shrink-0 items-center justify-between gap-3 bg-emerald-800 px-4 py-3 text-white">
          <div>
            <h2 id="profile-title" className="text-lg font-bold">Meu perfil</h2>
            <p id="profile-description" className="text-xs text-emerald-100">
              Personalize sua fazenda e seu avatar.
            </p>
          </div>
          <button
            ref={closeRef}
            type="button"
            onClick={close}
            disabled={state.saving}
            aria-label="Fechar meu perfil"
            className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-xl text-2xl hover:bg-white/15 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-300 disabled:opacity-50"
          >×</button>
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto p-4">
          <div className="mb-4 flex items-center gap-3 rounded-xl border border-emerald-200 bg-white p-3">
            <PlayerAvatar
              avatarId={state.draftAvatar}
              className="h-16 w-16"
              alt={`Prévia: ${getAvatar(state.draftAvatar).name}`}
            />
            <div className="min-w-0">
              <p className="truncate text-sm text-emerald-700">🌾 {trimmedFarmName || 'Nome da fazenda'}</p>
              <p className="truncate font-bold">👤 {user?.username}</p>
              {/* <p className="text-xs text-emerald-600">{getAvatar(state.draftAvatar).name}</p> */}
            </div>
          </div>

          <div className="mb-4">
            <label htmlFor="profile-farm-name" className="mb-1 block text-sm font-bold">
              Nome da fazenda
            </label>
            <input
              id="profile-farm-name"
              type="text"
              value={state.draftFarmName}
              maxLength={FARM_NAME_MAX_LENGTH}
              disabled={state.saving}
              aria-invalid={farmNameIsEmpty}
              aria-describedby="profile-farm-name-help"
              autoComplete="off"
              onChange={event => dispatch({
                type: 'edit-farm-name',
                farmName: event.target.value,
              })}
              className="min-h-11 w-full rounded-xl border border-emerald-300 bg-white px-3 py-2 text-sm text-emerald-950 outline-none transition placeholder:text-emerald-500 focus:border-emerald-600 focus:ring-4 focus:ring-yellow-300 disabled:opacity-60"
            />
            <div id="profile-farm-name-help" className="mt-1 flex justify-between gap-3 text-xs">
              <span className={farmNameIsEmpty ? 'font-semibold text-red-700' : 'text-emerald-700'}>
                {farmNameIsEmpty ? 'Digite um nome para sua fazenda.' : 'Este nome aparece nas visitas e para seus amigos.'}
              </span>
              <span className="shrink-0 text-emerald-600">
                {state.draftFarmName.length}/{FARM_NAME_MAX_LENGTH}
              </span>
            </div>
          </div>

          <fieldset disabled={state.saving}>
            <legend className="mb-2 text-sm font-bold">Escolha seu avatar · todos gratuitos</legend>
            <div className="grid grid-cols-3 gap-2 sm:grid-cols-5">
              {AVATARS.map(avatar => (
                <button
                  key={avatar.id}
                  type="button"
                  disabled={state.saving}
                  aria-pressed={state.draftAvatar === avatar.id}
                  onClick={() => dispatch({ type: 'select', avatarId: avatar.id })}
                  className={`flex min-h-20 min-w-11 flex-col items-center justify-center gap-1 rounded-xl border-2 p-1 text-xs font-semibold focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-400 disabled:opacity-60 ${state.draftAvatar === avatar.id ? 'border-emerald-700 bg-emerald-100' : 'border-emerald-200 bg-white hover:bg-emerald-100'}`}
                >
                  <PlayerAvatar avatarId={avatar.id} />
                  <span>{state.draftAvatar === avatar.id ? '✓ ' : ''}{avatar.name}</span>
                </button>
              ))}
            </div>
          </fieldset>

          {state.error && (
            <p role="alert" className="mt-3 rounded-lg border border-red-200 bg-red-50 p-2 text-sm text-red-700">
              {state.error}
            </p>
          )}
          {state.saving && (
            <p role="status" className="mt-3 text-sm text-emerald-700">Salvando seu perfil...</p>
          )}
        </div>

        <footer className="flex shrink-0 justify-end gap-2 border-t border-emerald-200 p-3">
          <button
            type="button"
            onClick={close}
            disabled={state.saving}
            className="min-h-11 rounded-xl border border-emerald-300 bg-white px-4 text-sm font-bold text-emerald-800 hover:bg-emerald-100 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-400 disabled:opacity-50"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={() => void save()}
            disabled={!canSaveProfile(state)}
            className="min-h-11 rounded-xl bg-emerald-700 px-4 text-sm font-bold text-white hover:bg-emerald-600 focus-visible:outline-none focus-visible:ring-4 focus-visible:ring-yellow-400 disabled:opacity-50"
          >
            {state.saving ? 'Salvando...' : state.error ? 'Tentar novamente' : 'Salvar'}
          </button>
        </footer>
      </div>
    </div>
  )
}
