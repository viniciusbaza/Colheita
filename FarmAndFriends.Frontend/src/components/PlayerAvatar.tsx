import cow from '../assets/avatars/cow.png'
import hen from '../assets/avatars/hen.png'
import pig from '../assets/avatars/pig.png'
import sheep from '../assets/avatars/sheep.png'
import horse from '../assets/avatars/horse.png'
import { getAvatar } from '../profile/avatarCatalog'

const images = { cow, hen, pig, sheep, horse }

export function PlayerAvatar({ avatarId, className = 'h-11 w-11', alt = '' }: {
  avatarId?: string | null
  className?: string
  alt?: string
}) {
  const avatar = getAvatar(avatarId)
  return (
    <img
      src={images[avatar.asset]}
      alt={alt}
      draggable={false}
      className={`shrink-0 rounded-full border border-emerald-200 bg-emerald-100 object-cover ${className}`}
    />
  )
}
