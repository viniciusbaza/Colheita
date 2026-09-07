export const AVATARS = [
  { id: 'avatar-1', name: 'Vaquinha', asset: 'cow' },
  { id: 'avatar-2', name: 'Galinha', asset: 'hen' },
  { id: 'avatar-3', name: 'Porquinho', asset: 'pig' },
  { id: 'avatar-4', name: 'Ovelha', asset: 'sheep' },
  { id: 'avatar-5', name: 'Cavalinho', asset: 'horse' },
] as const

export type AvatarId = (typeof AVATARS)[number]['id']

// Presentation fallback for older responses; the server validates all saves.
export function getAvatar(avatarId?: string | null) {
  return AVATARS.find(avatar => avatar.id === avatarId) ?? AVATARS[0]
}
