import { useState } from 'react'
import { loginRequest } from '../api/auth'
import { useAuth } from '../auth/useAuth'
import { useNavigate } from 'react-router-dom'

export default function Login() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { login } = useAuth()
  const navigate = useNavigate()

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)

    try {
      const response = await loginRequest({ username, password })
      localStorage.setItem('access_token', response.access_token)
      //alert('Login realizado com sucesso! 🎉')
      login(response.access_token) // Salva o token no contexto de autenticação
      navigate('/game') // Redireciona para a página do jogo
    } catch (err: any) {
      setError(err instanceof Error ? err.message : 'Erro desconhecido')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-green-900 to-emerald-700 px-4">
      <div className="w-full max-w-sm bg-zinc-900 rounded-2xl shadow-xl p-6 space-y-6">
        
        {/* Logo / Título */}
        <div className="text-center">
          <h1 className="text-3xl font-bold text-green-400">
            Farm & Friends
          </h1>
          <p className="text-zinc-400 text-sm mt-1">
            Entre para cuidar da sua fazenda 🌱
          </p>
        </div>

        {/* Formulário */}
        <form className="space-y-4" onSubmit={handleSubmit}>
          <div>
            <label className="block text-sm text-zinc-300 mb-1">
              Username
            </label>
            <input
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="Username"
              className="w-full rounded-lg bg-zinc-800 border border-zinc-700 px-3 py-2 text-zinc-100 focus:outline-none focus:ring-2 focus:ring-green-500"
              required
            />
          </div>

          <div>
            <label className="block text-sm text-zinc-300 mb-1">
              Senha
            </label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              className="w-full rounded-lg bg-zinc-800 border border-zinc-700 px-3 py-2 text-zinc-100 focus:outline-none focus:ring-2 focus:ring-green-500"
              required
            />
          </div>
          
          {error && (
            <p className="text-sm text-red-400 text-center">
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={loading}
            className="w-full bg-green-500 hover:bg-green-600 text-zinc-900 font-semibold py-2 rounded-lg transition"
          >
            {loading ? 'Entrando...' : 'Entrar'}
          </button>
        </form>

        {/* Footer */}
        <div className="text-center text-sm text-zinc-500">
          Ainda não tem conta?{" "}
          <span 
            className="text-green-400 hover:underline cursor-pointer"
            onClick={() => navigate('/register')}
          >
            Criar agora
          </span>
        </div>
      </div>
    </div>
  )
}
