import React from 'react'
import { type Friend } from '../types/Friend'

type FriendsPanelProps = {
  friends: Friend[]
  onClose: () => void
  onVisitFriend: (friend: Friend) => void
}

export const FriendsPanel: React.FC<FriendsPanelProps> = ({
  friends,
  onClose,
  onVisitFriend
}) => {
  return (
    <div 
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
      onPointerDown={(e) => e.stopPropagation()}
      onClick={(e) => e.stopPropagation()}
    >
      <div className="w-full max-w-md rounded-2xl bg-emerald-50 shadow-xl">

        {/* Header */}
        <div className="flex items-center justify-between rounded-t-2xl bg-emerald-600 px-4 py-3 text-white">
          <h2 className="text-lg font-bold">👥 Amigos</h2>
          <button
            onClick={onClose}
            className="rounded-full px-2 py-1 text-white hover:bg-white/20"
          >
            ✕
          </button>
        </div>

        {/* Add Friend */}
        <div className="border-b border-emerald-200 px-4 py-3">
          <button
            className="w-full rounded-xl border-2 border-dashed border-emerald-400 py-2 text-sm font-semibold text-emerald-700 hover:bg-emerald-100 transition"
          >
            + Adicionar Amigo
          </button>
        </div>

        {/* Friends List */}
        <div className="max-h-[420px] overflow-y-auto px-3 py-2">
          {friends.length === 0 ? (
            <div className="py-10 text-center text-sm text-emerald-700">
              Você ainda não tem amigos 😢
            </div>
          ) : (
            friends.map(friend => (
              <button
                key={friend.userId}
                onClick={(e) => {
                  e.stopPropagation()
                  onVisitFriend(friend)
                  onClose()
                }}
                className="mb-2 flex w-full items-center gap-3 rounded-xl bg-white p-3 text-left shadow-sm transition hover:scale-[1.02] hover:bg-emerald-100 active:scale-[0.98]"
              >
                {/* Avatar */}
                <div className="h-12 w-12 flex-shrink-0 overflow-hidden rounded-full bg-emerald-200">
                  {friend.avatarUrl ? (
                    <img
                      src={friend.avatarUrl}
                      alt={friend.username}
                      className="h-full w-full object-cover"
                    />
                  ) : (
                    <div className="flex h-full w-full items-center justify-center text-xl">
                      🙂 
                    </div>
                  )}
                </div>

                {/* Info */}
                <div className="flex flex-col">
                  <span className="font-semibold text-emerald-900">
                    {friend.username}
                  </span>
                  <span className="text-sm text-emerald-600">
                    {friend.farmName}
                  </span>
                </div>
              </button>
            ))
          )}
        </div>
      </div>
    </div>
  )
}
