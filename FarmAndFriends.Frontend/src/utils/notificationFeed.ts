import type { Friend } from '../types/Friend.ts'
import {
  NotificationType,
  type SocialNotification,
} from '../types/Social.ts'

export type NotificationCategory = 'farm' | 'friendships'

export type NotificationAction =
  | { kind: 'visit'; friend: Friend }
  | { kind: 'openFriendRequests' }

const VISITABLE_NOTIFICATION_TYPES = new Set<number>([
  NotificationType.CropCaredFor,
  NotificationType.PestRemoved,
  NotificationType.PestProtectionApplied,
  NotificationType.TheftOccurred,
  NotificationType.FriendRequestAccepted,
])

export function getNotificationCategory(
  notification: SocialNotification,
): NotificationCategory {
  switch (notification.type) {
    case NotificationType.FriendRequestReceived:
    case NotificationType.FriendRequestAccepted:
    case NotificationType.FriendRequestDeclined:
      return 'friendships'
    default:
      return 'farm'
  }
}

export function getNotificationAction(
  notification: SocialNotification,
  friends: Friend[],
  pendingIncomingRequestIds: ReadonlySet<string>,
): NotificationAction | null {
  if (notification.type === NotificationType.FriendRequestReceived) {
    return notification.friendshipId != null
      && pendingIncomingRequestIds.has(notification.friendshipId)
      ? { kind: 'openFriendRequests' }
      : null
  }

  if (
    !VISITABLE_NOTIFICATION_TYPES.has(notification.type) ||
    notification.actorUserId == null
  ) {
    return null
  }

  const friend = friends.find(
    currentFriend => currentFriend.userId === notification.actorUserId,
  )

  return friend ? { kind: 'visit', friend } : null
}

export function mergeNotificationFeedItems(
  current: SocialNotification[],
  incoming: SocialNotification[],
  windowStartUtc: string,
) {
  const windowStart = Date.parse(windowStartUtc)
  const byId = new Map(current.map(notification => [notification.id, notification]))

  for (const notification of incoming) {
    byId.set(notification.id, notification)
  }

  return [...byId.values()]
    .filter(notification => {
      const createdAt = Date.parse(notification.createdAt)
      return Number.isNaN(windowStart) || createdAt >= windowStart
    })
    .sort((left, right) => {
      const dateDifference = Date.parse(right.createdAt) - Date.parse(left.createdAt)
      return dateDifference || right.id.localeCompare(left.id)
    })
}
