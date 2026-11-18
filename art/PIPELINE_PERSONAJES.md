# Pipeline de personajes (referencia obligatoria)

Una sola estética para TODO el arte: **Fernando Botero** (volúmenes inflados y redondos, manos/pies/boca pequeños, expresión serena, óleo mate), adultos del Eje Cafetero, pies pegados al suelo con sombra de contacto. Los personajes son **muñecos articulados de piezas** (paper-doll con huesos), no cuadros horneados.

## Flujo (todo local, $0)
1. **Pose A por vista** con Flux schnell GGUF + LoRA Botero (ComfyUI propio, puerto 8189; ver memoria `botero-lora-installed`):
   `COMFY_HOST=http://127.0.0.1:8189 LORA=botero_flux.safetensors LORA_STRENGTH=1.6 python art/comfy/gen_apose.py <slot> <front|back|side> <semillas>`.
   Elegir UNA semilla cuyo frente/espalda/perfil tengan la misma ropa. Crudos en `art/incoming/` (no se versionan).
2. **Recorte de fondo**: `python art/comfy/doll_cut.py matte <png> art/doll/<n>/<vista>.png [mirror]` (el perfil debe mirar a la DERECHA; usar `mirror` si mira a la izquierda). `doll_cut.py grid` dibuja una cuadrícula para leer coordenadas.
3. **Piezas**: `art/doll/<n>/make_specs.py` define por vista los polígonos de cabeza, torso, brazos, muslos y botas, sus pivotes (articulaciones) y orden de dibujo; `doll_cut.py cut <n> <vista>` las recorta. Las piezas se solapan (muslo bajo el torso) para que no se vean cortes al girar.
4. **Empaque**: `python art/comfy/doll_pack.py <n>` → `game/Assets/Resources/Themes/Vintage/doll_<n>.json` + un atlas `.bytes` por vista.
5. **Runtime**: `PacmanDoll` arma la jerarquía de SpriteRenderers (cabeza/brazos hijos del torso, bota hija del muslo) y `PacmanDollPose` (clase pura, con tests) calcula la pose: caminata (balanceo de muslos, rodilla, pie levantado, brazos opuestos, rebote) e idle (respiración). `PacmanSpriteActor.Create` usa el doll si existe `doll_<n>`; si no, cae a las hojas `anim_<n>`.
6. Verificar: tira de poses con un `script eval` (cámara a RenderTexture) y una captura de la partida; en la web con `./hx web`.

## Reglas
- Todo el arte comparte el estilo Botero (personajes, frutos, matas, serpiente). Cambiar de estética = rehacer todo.
- Nada de arte "inventado": referencias colombianas reales (sombrero aguadeño, carriel, ruana, botas pantaneras). Prompts siempre "Colombia, Eje Cafetero".
- La serpiente La Coral sigue procedural (pendiente).
