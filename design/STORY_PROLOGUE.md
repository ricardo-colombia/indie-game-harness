# Prólogo en cómic (plan)

Siete viñetas, ~35 s, antes de la Finca 1. Se puede saltar (Enter, Esc, toque largo, botón "Saltar ▸"); se muestra una sola vez (`pacman.storyseen`) y la fila HISTORIA del menú la repite.

1. Amanecer en la cordillera (plano general).
2. Don Eliseo y su cafeto cargado. Misión: recoger todos los granos antes del anochecer.
3. La culebra coral: defiende su territorio (muerde de cerca).
4. El señor de los impuestos: quiere su parte (toma el dinero).
5. El guerrillero: pide "colaboración" para una guerra que Don Eliseo nunca pidió (sin armas en pantalla; megáfono).
6. El patrón: quiere comprar la finca (hace una oferta que no se acepta).
7. Cierre: los cuatro se acercan, "A COSECHAR".

Tono: fábula de campo, dignidad para el campesino, sin organizaciones reales, sin violencia explícita, sin glorificar al narco.
Técnica: rigs reales sobre escenario fuera de pantalla (como `PacmanTitleParade`); datos en una clase C# simple `PacmanStoryData` con pruebas EditMode; `PacmanStoryPlayer` (estado) y `PacmanStoryView` (dibujo).
Bocetos: artifact "Prólogo de Pac-Café" (https://claude.ai/artifact/GCcBJvLQuFibrqfhYpHVpi).
Etapas: 1 datos+pruebas, 2 dos viñetas, 3 las siete con movimiento y sonido, 4 saltar/repetir/táctil, 5 web y publicación.
