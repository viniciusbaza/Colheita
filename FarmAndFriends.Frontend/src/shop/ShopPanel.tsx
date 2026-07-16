import { useState } from 'react'
import { ShopBuyTab } from './ShopBuyTab'
import { ShopSellTab } from './ShopSellTab'
import { ShopFeedback } from './ShopFeedback'

type Props = {
  onClose: () => void
}

export function ShopPanel({ onClose }: Props) {
  const [tab, setTab] = useState<'buy' | 'sell'>('buy')
  const [feedback, setFeedback] = useState<{
    message: string
    type: 'success' | 'error'
  } | null>(null)

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
      onClick={onClose}
    >
      <div
        className="bg-amber-100 rounded-lg p-4 w-96 text-emerald-700"
        onClick={e => e.stopPropagation()}
      >
        <h2 className="font-bold mb-3">🏪 Loja</h2>

        {feedback && (
          <ShopFeedback
            message={feedback.message}
            type={feedback.type}
          />
        )}

        <div className="flex gap-2 mb-3">
          <button
            onClick={() => setTab('buy')}
            className={`flex-1 rounded py-1 ${
              tab === 'buy' ? 'bg-green-600 text-white' : 'bg-white'
            }`}
          >
            Comprar
          </button>

          <button
            onClick={() => setTab('sell')}
            className={`flex-1 rounded py-1 ${
              tab === 'sell' ? 'bg-amber-600 text-white' : 'bg-white'
            }`}
          >
            Vender
          </button>
        </div>

        {tab === 'buy' ? 
        (<ShopBuyTab
          onSuccess={msg => {
              setFeedback({ message: msg, type: 'success' })
              setTimeout(() => setFeedback(null), 2500)
          }}
          onError={msg => {
              setFeedback({ message: msg, type: 'error' })
              setTimeout(() => setFeedback(null), 3000)
          }}
        />) : 
        (<ShopSellTab 
          onSuccess={msg => {
              setFeedback({ message: msg, type: 'success' })
              setTimeout(() => setFeedback(null), 2500)
          }}
          onError={msg => {
              setFeedback({ message: msg, type: 'error' })
              setTimeout(() => setFeedback(null), 3000)
          }}
        />)}

        <button
          onClick={onClose}
          className="mt-3 w-full bg-red-500 text-white rounded py-1"
        >
          Fechar
        </button>
      </div>
    </div>
  )
}
