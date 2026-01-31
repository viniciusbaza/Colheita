// Regras de progressão de nível do jogador
// Mesma lógica do backend
export function xpToNextLevel(level: number): number {
  switch (level) {
    case 1:
      return 100
    case 2:
      return 250
    case 3:
      return 500
    default:
      return 1000 + level * 250
  }
}