const API_URL = 'http://localhost:5204'

let isRefreshing = false
let refreshPromise: Promise<void> | null = null

function forceLogout() {
  localStorage.removeItem('access_token')
  window.location.href = '/login'
}

async function refreshAccessToken(): Promise<boolean> {
  if (isRefreshing && refreshPromise) {
    await refreshPromise
    return true
  }

  isRefreshing = true

  refreshPromise = (async () => {
    const response = await fetch(`${API_URL}/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
    })

    if (!response.ok) {
      throw new Error('Refresh failed')
    }

    const data = await response.json()
    localStorage.setItem('access_token', data.access_token)
  })()

  try {
    await refreshPromise
    return true
  } catch {
    return false
  } finally {
    isRefreshing = false
    refreshPromise = null
  }
}

async function request<T>(
  input: string,
  init: RequestInit = {},
  auth = false,
  retry = true
): Promise<T> {
  const token = localStorage.getItem('access_token')

  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...(auth && token ? { Authorization: `Bearer ${token}` } : {}),
    ...init.headers,
  }

  console.log('FETCH →', `${API_URL}${input}`)

  const response = await fetch(`${API_URL}${input}`, {
    ...init,
    headers,
    credentials: 'include',
  })

  // 🔁 TOKEN EXPIRADO
  if (response.status === 401 && auth && retry) {
    const refreshed = await refreshAccessToken()

    if (!refreshed){
      forceLogout()
      throw new Error('Sessão expirada.')
    }

    return request<T>(input, init, auth, false)
  }

  if (!response.ok) {
    const raw = await response.text()
    throw new Error(raw)
  }

  if (response.status === 204) {
    return null as T
  }

  return response.json()
}

// ===== Públicos =====
export function post<T>(url: string, body: unknown) {
  return request<T>(url, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}

// ===== Autenticados =====
export function authFetch<T>(
  url: string,
  init: RequestInit = {}
) {
  return request<T>(url, init, true)
}
