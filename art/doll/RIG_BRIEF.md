# Brief: esqueletos reales para los 4 personajes (Don Eliseo, doctor, flaco, Hernán)

**Estado (web66, 0.1.66):** los muñecos se deforman con una malla de piel propia (`art/comfy/doll_skin.py` + `PacmanDoll.BuildSkin`).
Funciona, pero se hizo a mano. Defectos que quedan: pequeño desgarro cuando el brazo cercano pasa por delante de la cadera en la
vista lateral; en la vista frontal las manos salen abiertas con la palma hacia arriba (Flux dibuja los brazos extendidos).

**Meta:** reemplazar el rig a mano por un esqueleto hecho en software especializado, con pesos automáticos y un ciclo de caminata
estándar, sin inventar nada nuevo.

## Regla de oro (del director)
Partir de algo que ya funcione y modificarlo. Gratis y local. Estilo Botero, ropa colombiana siempre (ver memoria
`colombian-clothing-always`). No publicar en redes, no pagar APIs. Descargar un archivo o paquete: pedir permiso al director en el chat.

## Camino recomendado: Blender (ya instalado, hay MCP `mcp__Blender__*`)
1. Por cada personaje y vista (`art/doll/<name>/<view>.png`, 768 px de alto, fondo transparente) crear un plano subdividido
   (malla en rejilla ~10 px, recortada por el alfa) con la imagen como textura (material con `Image Texture` y alfa).
2. Añadir una armadura humana estándar (Rigify `Basic Human (Meta-Rig)` o una de 12 huesos: torso, cabeza, brazo/antebrazo/mano ×2,
   muslo/espinilla ×2) y colocar los huesos sobre el dibujo (articulaciones de `art/doll/make_pest_specs.py` y `farmer/make_specs.py`).
3. `Ctrl+P → With Automatic Weights` (heat diffusion). Es el estándar de la industria; reemplaza `doll_skin.py`.
4. Animar un ciclo de caminata de 8 cuadros (vistas frente, espalda y lado) con los brazos colgando: usar el ciclo de caminata de
   Rigify o el de Mixamo-style ya conocido; no inventar amplitudes. Brazos: el dibujo los tiene abiertos, así que primero poner
   un pose de reposo con los brazos hacia abajo (rotar huesos del hombro ~70-90°) y re-vincular en esa pose.
5. Salida más simple y robusta para Unity/WebGL: **renderizar los cuadros a hojas de sprites** (8 cuadros × 3 vistas × 4 personajes)
   con cámara ortográfica y fondo transparente, y que el juego reproduzca la hoja (`PacmanSpriteActor`). Sin deformación en
   tiempo real = sin desgarros. Alternativa: exportar FBX con malla skinneada si se prefiere deformar en Unity.
6. Validar con la tira de 8 cuadros ×4 direcciones y luego en el juego (`?v=` nuevo, a tamaño real ~110 px de alto).

## Alternativa de IA local (requiere descarga, pedir permiso)
Meta *Animated Drawings* (código abierto, corre local): toma un dibujo, genera esqueleto y animación de caminata. Sirve para probar
rápido la vista frontal; hay que revisar que respete el estilo y las proporciones Botero.

## Qué NO hacer
- No volver a `Sprite.OverrideGeometry` / `SpriteSkin` del paquete 2D Animation: no funciona en runtime (probado).
- No inventar ángulos de balanceo; usar los del ciclo estándar.
- No tocar `game/Packages/manifest.json`, `Assets/Settings/*`, `GraphicsSettings.asset`.

## Receta de publicación (preautorizada)
Ver el historial de `coffee-polish`: subir `bundleVersion`, `unity build --profile WebGL --output-path <dir> <ruta absoluta de coffee-wt/game>`,
copiar `Build/*` y `index.html` a la rama `gh-pages` (conservar la versión anterior, borrar la más vieja), verificar el `.wasm` con curl y
mirar el juego en vivo con `?v=` nuevo.
