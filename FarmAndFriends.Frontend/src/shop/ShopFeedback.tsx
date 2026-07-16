type Props = {
  message: string
  type?: 'success' | 'error'
}

export function ShopFeedback({ message, type = 'error' }: Props) {
  return (
    <div
      className={`mb-2 rounded px-2 py-1 text-sm text-center ${
        type === 'success'
          ? 'bg-green-500 text-white'
          : 'bg-red-500 text-white'
      }`}
    >
      {message}
    </div>
  )
}
