import { getAvatar, type AvatarId } from './avatarCatalog.ts'

export type ProfileEditorState = {
  savedAvatar: AvatarId
  savedFarmName: string
  draftAvatar: AvatarId
  draftFarmName: string
  saving: boolean
  error: string | null
}

export type ProfileEditorInitial = {
  avatarId?: string | null
  farmName?: string | null
}

export const FARM_NAME_MAX_LENGTH = 30

export type ProfileEditorAction =
  | { type: 'select'; avatarId: AvatarId }
  | { type: 'edit-farm-name'; farmName: string }
  | { type: 'save' }
  | { type: 'saved'; avatarId: string | null | undefined; farmName: string }
  | { type: 'failed'; message: string }
  | { type: 'cancel' }

export function createProfileEditor(initial: ProfileEditorInitial = {}): ProfileEditorState {
  const avatarId = getAvatar(initial.avatarId).id
  const farmName = initial.farmName ?? ''
  return {
    savedAvatar: avatarId,
    savedFarmName: farmName,
    draftAvatar: avatarId,
    draftFarmName: farmName,
    saving: false,
    error: null,
  }
}

export function getTrimmedFarmName(state: ProfileEditorState) {
  return state.draftFarmName.trim()
}

export function canSaveProfile(state: ProfileEditorState) {
  const farmName = getTrimmedFarmName(state)
  return !state.saving &&
    farmName.length > 0 &&
    farmName.length <= FARM_NAME_MAX_LENGTH &&
    (state.draftAvatar !== state.savedAvatar || farmName !== state.savedFarmName)
}

export function profileEditorReducer(
  state: ProfileEditorState,
  action: ProfileEditorAction,
): ProfileEditorState {
  switch (action.type) {
    case 'select':
      return state.saving
        ? state
        : { ...state, draftAvatar: action.avatarId, error: null }
    case 'edit-farm-name':
      return state.saving
        ? state
        : { ...state, draftFarmName: action.farmName, error: null }
    case 'save':
      return canSaveProfile(state)
        ? { ...state, saving: true, error: null }
        : state
    case 'saved':
      return createProfileEditor({
        avatarId: action.avatarId,
        farmName: action.farmName,
      })
    case 'failed':
      return { ...state, saving: false, error: action.message }
    case 'cancel':
      return state.saving
        ? state
        : createProfileEditor({
            avatarId: state.savedAvatar,
            farmName: state.savedFarmName,
          })
  }
}
