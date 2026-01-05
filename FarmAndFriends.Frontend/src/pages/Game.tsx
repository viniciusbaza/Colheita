import { useUser } from '../user/useUser'
import { PlayerHUD } from '../components/PlayerHUD' 
import { PhaserGame } from '../game/PhaserGame'

export default function Game() {
  const { user, loading } = useUser()
  
  // Estado de loading
  if (loading) {
    return (
      <div className="h-screen flex items-center justify-center bg-black text-white">
        <p>🔄 Carregando jogador...</p>
      </div>
    )
  }

  // Estado de erro
  if (!user) {
    return (
      <div className="h-screen flex items-center justify-center bg-red-900 text-white">
        <p>❌ Erro ao carregar jogador</p>
      </div>
    )
  }

  // Estado autenticado
  return (
    <div className="min-h-screen flex flex-col bg-green-100 text-white">

      {/* HUD */}
      <PlayerHUD />

      {/* Área do jogo (Phaser entra aqui depois) */}
      <div className="flex-1 flex items-center justify-center bg-green-200">
        <PhaserGame />
      </div>

    </div>
  )
}
