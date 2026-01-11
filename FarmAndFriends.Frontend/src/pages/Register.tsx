import { useState } from 'react'
import { registerRequest } from '../api/auth'
import { useNavigate } from 'react-router-dom'

export default function Register() {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [farmname, setFarmName] = useState('')
  const [confirmPassword, setConfirmPassword] = useState("")
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  
  const navigate = useNavigate()

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)

    // ✅ Validação de campos
    if (!username || !password || !confirmPassword || !farmname) {
      setError('Todos os campos são obrigatórios.')
      return
    }

    if (password !== confirmPassword) {
      setError('As senhas não coincidem.')
      return
    }

    if (!farmname.trim()) {
      setError('Informe o nome da fazenda')
      return
    }

    setLoading(true)

    try {
      const response = await registerRequest({ username, password, farmname })
      alert(`Registro realizado com sucesso! 🎉  Bem-vindo ${response.username} `)  
      navigate('/login') // Redireciona para a página do Login
    } catch (err: any) {
      // Mostra mensagem do backend ou fallback
      setError(err?.message || 'Erro ao registrar')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-900 text-white">
      <div className="bg-gray-800 p-8 rounded-lg w-full max-w-md shadow-lg space-y-6">
        
        {/* Logo / Título */}
        <div className="text-center">
          <h1 className="text-3xl font-bold text-green-400">
            Farm & Friends
          </h1>
          <p className="text-zinc-400 text-sm mt-1">
            Crie sua fazenda já! 🌱
          </p>
        </div>

        {/* Formulário */}
        <form className="space-y-4" onSubmit={handleSubmit}>
          <input
            type="text"
            placeholder="Username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            className="w-full mb-4 p-3 rounded bg-gray-700 placeholder-gray-400"
          />
          <input
            type="password"
            placeholder="Senha"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="w-full mb-4 p-3 rounded bg-gray-700 placeholder-gray-400"
          />
          <input
            type="password"
            placeholder="Confirmar Senha"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            className="w-full mb-4 p-3 rounded bg-gray-700 placeholder-gray-400"
          />
          <input
            type="text"
            placeholder="Nome da fazenda"
            value={farmname}
            onChange={e => setFarmName(e.target.value)}
            className="w-full mb-4 p-3 rounded bg-gray-700 placeholder-gray-400"
          />
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
            {loading ? 'Registrando...' : 'Criar Conta'}
          </button>
        </form>

        {/* Footer */}
        <div className="text-center text-sm text-zinc-500">
          Já tem conta?{" "}
          <span 
            className="text-green-400 hover:underline cursor-pointer"
            onClick={() => navigate('/login')}
          >
            Entrar
          </span>
        </div>
      </div>
    </div>
  )
}
