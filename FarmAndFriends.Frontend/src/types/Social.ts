export const FriendshipStatus = {
  Pending: 0,
  Accepted: 1,
  Declined: 2,
  Cancelled: 3,
} as const

export type FriendshipStatus =
  (typeof FriendshipStatus)[keyof typeof FriendshipStatus]

export const NotificationType = {
  FriendRequestReceived: 0,
  FriendRequestAccepted: 1,
  FriendRequestDeclined: 2,
  TheftOccurred: 3,
  CropCaredFor: 4,
} as const

export type NotificationType =
  (typeof NotificationType)[keyof typeof NotificationType]

export type UserSearchResult = {
  id: string
  username: string
}

export type FriendRequest = {
  id: string
  requesterUserId: string
  requesterUsername: string
  recipientUserId: string
  recipientUsername: string
  status: FriendshipStatus
  createdAt: string
  respondedAt: string | null
}

export type SocialNotification = {
  id: string
  type: NotificationType
  actorUserId: string | null
  actorUsername: string | null
  message: string | null
  friendshipId: string | null
  theftLogId: string | null
  careOpportunityId: string | null
  createdAt: string
  readAt: string | null
}

export type UnreadCountResponse = {
  count: number
}
