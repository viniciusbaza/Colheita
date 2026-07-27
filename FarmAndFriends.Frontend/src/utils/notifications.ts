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
    default:
      return 'Nova notificação'
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
    default:
      return 'Você recebeu uma nova notificação.'
  }
}
