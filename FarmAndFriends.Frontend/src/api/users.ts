import { authFetch } from './http'
import type { AvatarId } from '../profile/avatarCatalog'
import type { User } from '../user/UserContext'
import { withAvatarSaveDeadline } from './avatarSaveDeadline'

export type UpdateProfileRequest = {
  avatarId: AvatarId
  farmName: string
}

export type UpdateProfileResponse = {
  avatarId: string
  farmId: string
  farmName: string
}

export function getCurrentUser() {
  return authFetch<User>('/users/me')
}

export function updateProfile(profile: UpdateProfileRequest) {
  return withAvatarSaveDeadline(signal => authFetch<UpdateProfileResponse>('/users/me/profile', {
    method: 'PATCH',
    signal,
    body: JSON.stringify(profile),
  }))
}
