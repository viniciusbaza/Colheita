type Props = {
  message: string
  type?: 'success' | 'error'
}

export function ShopFeedback({ message, type = 'error' }: Props) {
  return (
    <div
      role={type === 'error' ? 'alert' : 'status'}
      className={`rounded-lg border px-3 py-2 text-sm ${
        type === 'success'
          ? 'border-emerald-300 bg-emerald-100 text-emerald-800'
          : 'border-red-300 bg-red-50 text-red-700'
      }`}
    >
      {message}
    </div>
  )
}
