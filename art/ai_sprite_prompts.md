# PAC-CAFÉ — hoja de prompts para sprites (ChatGPT / Gemini)

Flujo: pega el **prefijo de estilo** + el prompt del sprite en la app, descarga el PNG y guárdalo en
`art/incoming/<nombre>.png` (el nombre exacto de la lista). Yo recorto el fondo, ajusto el tamaño e
importo el sprite al juego. Nada entra al juego sin tu visto bueno de gusto.

## Prefijo de estilo — tema Vintage (pegar siempre, para que todo se vea de la misma familia)

> Genera una imagen cuadrada 1:1. Sprite de videojuego en estilo ILUSTRACIÓN DE ETIQUETA VINTAGE de café
> colombiano: grabado con trazos finos de tinta y sombreado con líneas, pocas tintas planas (rojo, verde
> oliva, crema, marrón), contorno marrón oscuro cerrado. Un solo objeto centrado, sobre un fondo BLANCO PURO
> #FFFFFF liso que llena toda la imagen hasta los bordes, sin marco, sin papel, sin texto, sin sombra
> proyectada. Objeto: …

Fondo **blanco**, no verde: el verde tiñe los objetos. El recorte se hace con `cutout.py` (relleno desde los bordes).
Para los siguientes objetos, pedir "con exactamente el mismo estilo que la imagen anterior".
Los temas viven en `game/Assets/Resources/Themes/<Id>/` (hoy: `Vintage`); lo que falta lo dibuja el arte procedural.

## Elementos estáticos (empezar por estos)

| Archivo | Prompt |
|---|---|
| `bean.png` | Un grano de café cereza recién cosechado, rojo brillante con un pequeño brote de hoja verde. |
| `mug.png` | Una taza de barro blanca con café humeante, vista 3/4, vapor suave. Es el power-up. |
| `fruit_coffeecherry.png` | Un racimo de dos cerezas de café rojas con hojas verdes. |
| `fruit_banana.png` | Un plátano maduro amarillo. |
| `fruit_arepa.png` | Una arepa dorada redonda con un poco de mantequilla. |
| `fruit_panela.png` | Un bloque de panela marrón dorado. |
| `fruit_tinto.png` | Una tacita de tinto (café negro) humeante. Distinta de la taza del power-up: más pequeña y sin plato. |
| `fruit_sack.png` | Un costal de fique de café, atado arriba, con una cereza de café dibujada. |
| `fruit_sombrero.png` | Un sombrero aguadeño blanco con cinta negra. |

## Notas
- Los personajes animados (campesino y pestes) siguen con el rig procedural: la IA no repite el mismo dibujo entre cuadros.
- Steam exige declarar el uso de IA en la ficha de la tienda si estos sprites llegan al juego final.
