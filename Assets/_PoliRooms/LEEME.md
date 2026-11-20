# PoliRooms — Nivel 1 (La Pecera)

## Cómo generar y probar el nivel

1. Abrí Unity y esperá a que termine de compilar los scripts.
2. Menú **PoliRooms → Construir Nivel 1 (Pecera)**.
   - Saca a Vacalluzzo de `SampleScene`.
   - Crea `Assets/_PoliRooms/Escenas/N1_Pecera.unity` usando `pecera.blend` y la pone primera en Build Settings.
3. Dale **Play**.

Si cambian `pecera.blend`, vuelvan a correr el menú: la escena se rearma desde cero.
Lo que agreguen a mano en `N1_Pecera.unity` se pierde al reconstruir.

## Controles

| Acción | Teclado | Joystick |
|---|---|---|
| Moverse / levantarse del banco | WASD | Stick izq. |
| Mirar | Mouse | Stick der. |
| Correr | Shift | Click stick izq. |
| Agacharse | C o Ctrl | B / Círculo |
| Interactuar (tocar o mantener) | E | A / Cruz |
| Flash | F o click derecho | Gatillo der. |
| Pausa | Esc | Start |

## Flujo del nivel

1. Gonza te pide el celular (E) y lo guarda en la gaveta con candado.
2. Mayra corrige pruebas. Te levantás en sigilo; si te escucha y no estás en tu banco, te busca.
3. La llave está abajo de un banco al azar (tintineo muy bajito). Agachado + mantener E para revisar.
4. Abrís la gaveta: recuperás el celular y Mayra se planta en la puerta.
5. La encandilás con el flash (5 s), salís y doblás a la derecha por el pasillo.
6. Cinemática del preceptor y piña → "Nivel 2: Laboratorio".

## Dónde ajustar valores (Inspector)

- **Jugador → ControladorJugador**: velocidades, estamina, sensibilidad.
- **Jugador → SistemaRuido**: radios de ruido (agachado 2 m, caminando 6 m, corriendo 14 m) y micrófono (+10 m máx).
- **Jugador → FlashCelular**: perfiles Nokia y Celular (alcance, ángulo, aturdimiento, recarga).
- **Mayra → ProfesoraMayra**: visión, velocidades, tiempos, drenaje de cordura.
- **Sistemas → Cordura**: umbral del blur (30 %).

Si un modelo queda de espaldas, cambiar `GiroMayra` / `GiroGonza` / `GiroPreceptor` a 180 en
`Assets/_PoliRooms/Editor/ConstructorNivel1.cs` y reconstruir.

## Escala

`pecera.blend` está a **1.6x** la escala real (un pupitre mide 1.21 m). Todo lo demás está igualado
a esa escala: jugador (2.88 m de alto, ojos a 2.56 m), personajes, velocidades, distancias de ruido,
gaveta y pasillo. Constante `E` en `ConstructorNivel1.cs`.

## Detalles en pecera.blend

Los detalles del aula sacados de las fotos (pizarrón, reloj, escritorio de Mayra, cerchas, ventanas
con cortinas azules, aires, franja naranja, paneles beige, proyector, TV, armario, panel azul, tachos,
marcos de ventanas) están en la colección **"Detalles"** del .blend. Se pueden mover o borrar libremente.

## Animaciones (Mixamo) — `Assets/_PoliRooms/Animaciones`

Se importan solas como Humanoid (ver `Editor/ImportadorPoliRooms.cs`). El constructor arma
`ControladorPersonaje.controller` con parámetros `Velocidad` y `Accion`.

| Situación | Animación |
|---|---|
| Mayra en la intro, sentada | Sentado |
| Mayra corrigiendo | Escribiendo |
| Mayra levanta la vista / escuchó algo | SentadoDesaprobacion |
| Mayra se para para buscarte | Levantarse |
| Mayra buscándote | CaminarSigilo |
| Mayra patrullando / persiguiendo / en la puerta | Caminar (más rápido al perseguir) |
| Gonza | Idle del modelo + Caminar |
| Preceptor | CaminarPesado |

Sin usar por ahora: Sentado2, EmpezarACaminar, DejarDeCaminar, CaminarAgachado (el estado existe).

## Sonidos — `Assets/_PoliRooms/Audio`

| Situación | Archivo |
|---|---|
| Puerta del aula abriéndose | PuertaAbrir.mp3 |
| Pasos de Mayra (tacos) | PasosTacos.wav |
| Pasos del jugador (zapatillas, sigilo) | PasosZapatillasSigilo.wav |
| Pasos de Gonza y del preceptor | PasosZapatillas.wav |
| Llave escondida (tintineo) y al encontrarla | LlaveTintineo.wav |
| Abrir el candado de la gaveta (mientras mantenés E) | LlaveCerradura.wav |
| Mayra escucha algo | VozMujerSospecha.wav |
| Gonza pide/insiste, el preceptor te descubre | VozHombreHmm.wav |

El resto (flash, piña, latidos, lapicera, silla, puerta cerrándose, bisagra) sigue generado por código.

## Modelos de los personajes

Están en `Assets/_PoliRooms/Modelos/Riggeados/<Nombre>/<Nombre>.fbx` con su textura `<Nombre>.png`
al lado. Se importan solos como Humanoid (rig de Mixamo) y el constructor les pone su textura.

| Personaje en el Nivel 1 | Modelo |
|---|---|
| Mayra | Mayra (Open Arms Portrait) |
| Gonza | Gonza (Man in Olive Sweater) |
| Preceptora del final | Laura (Turquoise Welcome) |

Listos para los próximos niveles: Barbieri, Marinelli y Vacalluzzo.
Si alguno queda de espaldas, cambiar `GiroMayra` / `GiroGonza` / `GiroPreceptor` a 180 en `ConstructorNivel1.cs`.
