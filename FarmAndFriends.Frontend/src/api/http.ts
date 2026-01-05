const API_URL = 'http://localhost:5204'

async function request<T>(
  input: string,
  init: RequestInit = {},
  auth = false
): Promise<T> {
  const token = localStorage.getItem('access_token')

  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...(auth && token ? { Authorization: `Bearer ${token}` } : {}),
    ...init.headers,
  }

  //console.log('FETCH →', `${API_URL}${input}`)

  const response = await fetch(`${API_URL}${input}`, {
    ...init,
    headers,
  })

  if (!response.ok) {
    // tenta ler json de erro, se não der cai no text
    let message = response.status.toString()
    try {
      const error = await response.json()
      message = error.message ?? JSON.stringify(error)
    } catch {
      message = await response.text()
    }

    throw new Error(message || response.status.toString())
  }

  // ⚠️ Alguns endpoints podem retornar 204 no futuro
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
