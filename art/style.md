# Art direction (director-owned; agents read)

## Prompt prefix
stylized low-poly game asset, soft hand-painted look, warm saturated palette, clean readable silhouette,

## Negative / avoid
photorealism, text, logos, watermarks, busy backgrounds, cropped objects

## Palette & references
(director: add hex colours and 3–5 reference images in art/refs/)

## Technical
1 unit = 1 m. Hero props ≤ 8k tris, small props ≤ 2k. Textures 1024² (hero 2048²). URP Lit materials.

## Coffee theme: Vintage "label engraving" (2D)
Chosen by the director. Closed dark-brown ink outline (about #331B10), flat muted inks with line hatching in the shadows, paper grain, warm earth palette (cherry red, straw, burlap, leaf green). Pre-drawn sprites (pellet cherry, power-up mug, 7 bonus fruits) are AI generated on a pure white background and cut out; characters, bushes, mountains and floor are painted by code with the same finish. Prompts: `art/ai_sprite_prompts.md`. A second "Clásico" look (plain procedural art) stays selectable in the title menu.

## Personajes pintados (ComfyUI local)
Don Eliseo, El Doctor, Alias El Flaco y Don Hernán son imágenes generadas con ComfyUI (SDXL base, local, gratis) como "sticker design of a chibi cartoon character": `art/comfy/gen_chars.py <slot> [semillas]` genera en `art/incoming/`, y `art/comfy/cutout_chars.py` recorta el fondo y deja `game/Assets/Resources/Themes/Vintage/char_<slot>.png` con borde crema. El juego usa esos PNG si existen (`PacmanTheme.Get`); si faltan, vuelve a los muñecos procedurales. La Coral (serpiente) sigue procedural. Steam exige declarar el uso de IA si estos sprites llegan al juego final. Nunca vaciar la cola compartida de ComfyUI.
