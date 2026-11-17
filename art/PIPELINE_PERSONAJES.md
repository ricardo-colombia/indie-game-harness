# Pipeline de personajes (referencia obligatoria)

Decidido tras investigar cómo lo hacen estudios reales; no improvisar fuera de esto.

## Qué hacen los juegos de referencia
- **Dead Cells**: modelos 3D + animación, renderizados a sprites 2D con herramienta propia ([80.lv](https://80.lv/articles/case-study-dead-cells-character-art-pipeline)).
- **Hades**: concept art (Jen Zee) → modelo 3D con línea pintada → render a sprites ([Game Developer](https://gamedeveloper.com/art/learn-how-supergiant-brought-i-hades-i-hand-painted-characters-to-life)).
- **Rayman Legends (UbiArt)**: ilustración por partes + huesos; **Klei**: partes × vistas.
- Regla común: el personaje es un objeto con volumen, mira hacia donde camina, usa una animación hecha por un animador o captura de movimiento, y comparte iluminación/estilo con la escena.

## Nuestro flujo (todo local, $0)
1. **Concepto**: hoja de Flux (`art/comfy/gen_flux.py`) o referencia real del personaje colombiano. Una sola estética: vitrina de etiqueta vintage de café, gouache, contorno de tinta.
2. **Modelo 3D** en Blender 5.3 headless (`art/blender/person.py` + un script por personaje). Proporciones chibi (cabeza grande, siluetas legibles), partes con volumen, ropa específica de Colombia. Contorno por casco invertido; sombreado toon de 3 pasos (EEVEE).
3. **Animación**: caminata real de captura de movimiento (CMU Mocap, `art/mocap/cmu_02_01_walk.bvh`, uso comercial libre, ver `CMU_READMEFIRST.txt`) retargeteada con `art/blender/mocap.py` (apunta cada hueso en la dirección capturada; torso y cabeza atenuados). Idle por ciclo respirando. Nunca senos escritos a mano para la caminata.
4. **Render**: cámara ortográfica a 40° (como Hades/Zelda), 3 direcciones (s, e, n; oeste = este espejado), 8 cuadros de caminata + 8 de idle, `art/blender/pack.py` empaqueta a `anim_<nombre>.png/.json` (potencia de 2, sin mipmaps).
5. **Unity**: `PacmanSpriteActor` elige fila por dirección de movimiento y cuadro por la fase del paso; cero asignaciones por frame.

## Limitaciones conocidas / siguientes
- Modelos hechos con primitivas: se ven correctos pero simples; mejorar con referencia ortográfica de Flux, más forma y texturas pintadas.
- Image-to-3D local (TRELLIS.2 / Hunyuan3D en Apple Silicon) es viable en teoría, pero exige 24 GB+ libres y compite con Ollama; no usado todavía.
- Mixamo es el estándar de la industria para auto-rig + animaciones, pero requiere cuenta Adobe del director.
