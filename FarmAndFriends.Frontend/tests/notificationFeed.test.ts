import assert from 'node:assert/strict'
import test from 'node:test'
import type { Friend } from '../src/types/Friend.ts'
import {
  NotificationType,
  type SocialNotification,
} from '../src/types/Social.ts'
import {
  getNotificationAction,
  getNotificationCategory,
  mergeNotificationFeedItems,
} from '../src/utils/notificationFeed.ts'

const friend: Friend = {
  userId: 'friend-1',
  username: 'Ana',
  farmId: 'farm-1',
  farmName: 'Fazenda da Ana',
}

function notification(
  id: string,
  type: SocialNotification['type'],
  overrides: Partial<SocialNotification> = {},
): SocialNotification {
  return {
    id,
    type,
    actorUserId: friend.userId,
    actorUsername: friend.username,
    message: null,
    friendshipId: null,
    theftLogId: null,
    careOpportunityId: null,
    pestPlotId: null,
    createdAt: '2026-09-01T12:00:00.000Z',
    readAt: null,
    ...overrides,
  }
}

test('offers a visit only for the five approved event types and a current friend', () => {
  const visitableTypes = [
    NotificationType.CropCaredFor,
    NotificationType.PestRemoved,
    NotificationType.PestProtectionApplied,
    NotificationType.TheftOccurred,
    NotificationType.FriendRequestAccepted,
  ]

  for (const type of visitableTypes) {
    assert.deepEqual(
      getNotificationAction(
        notification(String(type), type),
        [friend],
        new Set(),
      ),
      { kind: 'visit', friend },
    )
  }
})

test('hides visit actions without an actor or an accepted current friendship', () => {
  assert.equal(
    getNotificationAction(
      notification('no-actor', NotificationType.PestRemoved, {
        actorUserId: null,
        actorUsername: null,
      }),
      [friend],
      new Set(),
    ),
    null,
  )

  assert.equal(
    getNotificationAction(
      notification('former-friend', NotificationType.TheftOccurred),
      [],
      new Set(),
    ),
    null,
  )

  assert.equal(
    getNotificationAction(
      notification('automatic', NotificationType.PestConsumed, {
        actorUserId: null,
      }),
      [friend],
      new Set(),
    ),
    null,
  )
})

test('routes only still-pending received requests to the requests panel', () => {
  const received = notification(
    'received',
    NotificationType.FriendRequestReceived,
    { friendshipId: 'request-1' },
  )

  assert.deepEqual(
    getNotificationAction(received, [], new Set(['request-1'])),
    { kind: 'openFriendRequests' },
  )

  assert.equal(getNotificationAction(received, [], new Set()), null)

  assert.equal(
    getNotificationAction(
      notification('declined', NotificationType.FriendRequestDeclined),
      [friend],
      new Set(),
    ),
    null,
  )
})

test('separates farm events from friendship events', () => {
  assert.equal(
    getNotificationCategory(
      notification('care', NotificationType.CropCaredFor),
    ),
    'farm',
  )
  assert.equal(
    getNotificationCategory(
      notification('request', NotificationType.FriendRequestReceived),
    ),
    'friendships',
  )
})

test('merges pages by id, keeps the freshest payload, and removes expired items', () => {
  const current = [
    notification('duplicate', NotificationType.CropCaredFor, {
      createdAt: '2026-08-31T10:00:00.000Z',
      readAt: null,
    }),
    notification('expired', NotificationType.PestConsumed, {
      createdAt: '2026-08-24T09:59:59.000Z',
    }),
  ]
  const incoming = [
    notification('newer', NotificationType.PestRemoved, {
      createdAt: '2026-09-01T10:00:00.000Z',
    }),
    notification('duplicate', NotificationType.CropCaredFor, {
      createdAt: '2026-08-31T10:00:00.000Z',
      readAt: '2026-09-01T11:00:00.000Z',
    }),
  ]

  const merged = mergeNotificationFeedItems(
    current,
    incoming,
    '2026-08-24T10:00:00.000Z',
  )

  assert.deepEqual(merged.map(item => item.id), ['newer', 'duplicate'])
  assert.equal(merged[1]?.readAt, '2026-09-01T11:00:00.000Z')
})

test('uses the id as a stable tiebreaker for equal timestamps', () => {
  const merged = mergeNotificationFeedItems(
    [],
    [
      notification('a', NotificationType.CropCaredFor),
      notification('b', NotificationType.TheftOccurred),
    ],
    '2026-08-25T12:00:00.000Z',
  )

  assert.deepEqual(merged.map(item => item.id), ['b', 'a'])
})
