export function gridToIso(
  x: number,
  y: number,
  tileWidth: number,
  tileHeight: number
) {
  return {
    isoX: (x - y) * (tileWidth / 2),
    isoY: (x + y) * (tileHeight / 2),
  }
}