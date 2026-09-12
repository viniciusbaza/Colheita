## Perennial crop reference

Inspect an existing multi-harvest or perennial implementation before designing one. Define planting cost, first-growth timing, cycle timing, yield per cycle, harvest reset, lifespan or removal rule, inventory/sale behavior, and persistence. Keep `isReady` and all rewards authoritative. Preserve the visual sequence `plot-growing → sprout → mature → ready`; do not materialize a post-harvest crop sprite until the harvest animation completes when the existing flow requires it.
