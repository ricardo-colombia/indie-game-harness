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
Don Eliseo, El Doctor, Alias El Flaco y Don Hernán son pinturas 2D de caricatura (estilo Rayman Legends, paleta cálida del cartel de café), generadas con ComfyUI (SDXL base, local, gratis) y animadas con esqueleto y malla deformable, no como imágenes fijas. Pipeline: `art/comfy/gen_chars.py <slot> <semillas> legends3` genera en `art/incoming/` → `art/comfy/cutout_chars.py <png> <slot> matte` recorta con rembg (isnet) y deja `game/Assets/Resources/Themes/Vintage/char_<slot>.png` → `art/comfy/make_rig.py <slot>` calcula la malla y los pesos de los huesos (`char_<slot>.rig.json`; las articulaciones se colocan a mano en `RIGS`). En el juego `PacmanSkinRig` deforma la malla (skinning lineal, sin basura por cuadro), `PacmanSkinPuppet` la dibuja y `PacmanPuppetPose` da el ciclo de caminata compartido (zancada, rebote, aplastamiento, sombrero y manos con retardo). Si falta el PNG o el rig, vuelve al sprite fijo o a los muñecos procedurales. Todos los personajes deben compartir un solo estilo: compararlos lado a lado antes de publicar. El campesino debe leerse colombiano del Eje Cafetero (sombrero aguadeño, carriel, ruana, pantalón arremangado, botas de caucho). La Coral (serpiente) sigue procedural. Steam exige declarar el uso de IA si estos sprites llegan al juego final. Nunca vaciar la cola compartida de ComfyUI.
