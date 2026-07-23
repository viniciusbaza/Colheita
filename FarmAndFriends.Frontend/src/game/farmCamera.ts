export const FARM_CAMERA_MODE_EVENT = 'farm:camera:mode'

export type FarmCameraMode = 'focus' | 'overview'

export type FarmCameraModeDetail = {
  mode: FarmCameraMode
}

export function dispatchFarmCameraMode(mode: FarmCameraMode) {
  window.dispatchEvent(
    new CustomEvent<FarmCameraModeDetail>(FARM_CAMERA_MODE_EVENT, {
      detail: { mode },
    }),
  )
}
