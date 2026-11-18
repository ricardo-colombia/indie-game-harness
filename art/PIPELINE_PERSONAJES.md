# Pipeline de personajes (referencia obligatoria)

Una sola estética para todo el reparto: **ilustración vintage de etiqueta de café colombiano, pintada a mano (gouache + línea de tinta)**, adultos con proporciones reales, pies pegados al suelo con sombra de contacto. El 3D "chibi" (Blender) fue rechazado: no se usa.

## Flujo (todo local, $0)
1. **Hoja de giro** con Flux schnell GGUF en el ComfyUI propio (puerto 8189): `art/comfy/gen_flux.py` (frente / perfil derecho / espalda en una sola imagen = un solo personaje). Revisar y elegir la hoja; los crudos quedan en `art/incoming/` (no se versionan).
2. **Recorte**: `art/comfy/pest_cuts.py <nombre> front=<hoja>:<n> side=<hoja>:<n> back=<hoja>:<n>` (rembg + fondo plano de papel; las camisas blancas se solidifican). Salida: `art/pest_cuts/<nombre>/{front,side,back}.png`. El cafetero (`art/cafetero_cuts/`) se cortó igual.
3. **Huesos + malla**: `art/comfy/bake_walk.py <cortes> <salida> <nombre>`. Los rigs del cafetero están colocados a mano (`RIGS`); para las plagas `AUTO_RIG=1` los deduce de la silueta. Skinning lineal (malla Delaunay), ciclo de caminata y reposo horneados a cuadros.
4. **Empaque**: `art/blender/pack.py <cuadros> <nombre>` → `game/Assets/Resources/Themes/Vintage/anim_<nombre>.png/.json`. `PacmanSpriteActor` elige fila por dirección (s/e/n; oeste = este espejado).
5. Verificar en el editor (una captura) y en la web (`./hx web`).

## Reglas
- Nada de arte "inventado": referencias reales colombianas (sombrero aguadeño, carriel, ruana, botas pantaneras).
- Cambiar de estética = rehacer todo el reparto, no un personaje.
- La serpiente La Coral sigue procedural (pendiente de pasar a este flujo).
