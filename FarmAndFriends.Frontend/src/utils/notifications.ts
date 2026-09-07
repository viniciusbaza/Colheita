import { NotificationType, type SocialNotification } from '../types/Social'

export function getNotificationTitle(notification: SocialNotification) {
  switch (notification.type) {
    case NotificationType.FriendRequestReceived:
      return 'Nova solicitação de amizade'
    case NotificationType.FriendRequestAccepted:
      return 'Solicitação aceita'
    case NotificationType.FriendRequestDeclined:
      return 'Solicitação recusada'
    case NotificationType.TheftOccurred:
      return 'Roubo na fazenda'
    case NotificationType.CropCaredFor:
      return 'Cuidado na plantação'
    case NotificationType.PestConsumed:
      return 'Lagarta na colheita'
    case NotificationType.PestRemoved:
      return 'Lagarta removida'
    case NotificationType.PestProtectionApplied:
      return 'Lote protegido'
    default:
      return 'Novo acontecimento'
  }
}

export function getNotificationIcon(notification: SocialNotification) {
  switch (notification.type) {
    case NotificationType.FriendRequestReceived:
      return '👋'
    case NotificationType.FriendRequestAccepted:
      return '🤝'
    case NotificationType.FriendRequestDeclined:
      return '👥'
    case NotificationType.TheftOccurred:
      return '🥷'
    case NotificationType.CropCaredFor:
      return '💧'
    case NotificationType.PestConsumed:
    case NotificationType.PestRemoved:
      return '🐛'
    case NotificationType.PestProtectionApplied:
      return '🛡️'
    default:
      return '🔔'
  }
}

export function getNotificationMessage(notification: SocialNotification) {
  if (notification.message?.trim()) {
    return notification.message
  }

  const actor = notification.actorUsername ?? 'Um jogador'

  switch (notification.type) {
    case NotificationType.FriendRequestReceived:
      return `${actor} enviou uma solicitação de amizade.`
    case NotificationType.FriendRequestAccepted:
      return `${actor} aceitou sua solicitação de amizade.`
    case NotificationType.FriendRequestDeclined:
      return `${actor} recusou sua solicitação de amizade.`
    case NotificationType.TheftOccurred:
      return `${actor} roubou um item da sua fazenda.`
    case NotificationType.CropCaredFor:
      return `${actor} cuidou de uma plantação na sua fazenda.`
    case NotificationType.PestConsumed:
      return 'Uma lagarta atacou sua plantação e parte da colheita foi perdida.'
    case NotificationType.PestRemoved:
      return `${actor} removeu uma lagarta da sua plantação.`
    case NotificationType.PestProtectionApplied:
      return `${actor} protegeu um lote da sua fazenda contra pragas.`
    default:
      return 'Um novo acontecimento foi registrado.'
  }
}
