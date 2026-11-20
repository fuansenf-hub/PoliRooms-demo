using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace PoliRooms.Editor
{
    /// <summary>
    /// Arma la escena del Nivel 1 (La Pecera) a partir del modelo pecera.blend del equipo:
    /// gaveta, escritorio de Mayra, pizarrón, pasillo de salida, luces, NavMesh, personajes,
    /// jugador, HUD y el guion del nivel. Menú: PoliRooms → Construir Nivel 1 (Pecera).
    ///
    /// Las posiciones están escritas en coordenadas de Blender (las del .blend: X/Y en el piso,
    /// Z hacia arriba) y se convierten a Unity detectando cómo quedó importado el modelo.
    /// </summary>
    public static class ConstructorNivel1
    {
        const string RutaEscena = "Assets/_PoliRooms/Escenas/N1_Pecera.unity";
        const string CarpetaEscena = "Assets/_PoliRooms/Escenas";
        const string CarpetaMateriales = "Assets/_PoliRooms/Materiales";
        const string RutaPecera = "Assets/pecera.blend";
        const string RutaSampleScene = "Assets/Scenes/SampleScene.unity";

        // Modelos reales de los profes, riggeados con Mixamo (Humanoid). Cada uno con su textura
        // (misma carpeta, mismo nombre .png). En Riggeados también están Barbieri, Marinelli y Vacalluzzo.
        const string CarpetaRiggeados = ImportadorPoliRooms.CarpetaRiggeados;
        const string RutaMayra = CarpetaRiggeados + "Mayra/Mayra";
        const string RutaGonza = CarpetaRiggeados + "Gonza/Gonza";
        const string RutaPreceptor = CarpetaRiggeados + "Laura/Laura"; // Laura es la preceptora del final

        const string CarpetaAnim = "Assets/_PoliRooms/Animaciones/";
        const string RutaControladorAnim = CarpetaAnim + "ControladorPersonaje.controller";
        const string RutaIdle = CarpetaAnim + "Quieto.fbx"; // copia del Idle del modelo viejo, con pies en el piso
        const string CarpetaAudio = "Assets/_PoliRooms/Audio/";

        // Si algún modelo queda mirando para atrás, cambiar a 180.
        const float GiroMayra = 0f, GiroGonza = 0f, GiroPreceptor = 0f;

        /// <summary>pecera.blend está a 1.6x la escala real: personas y objetos agregados se escalan igual.</summary>
        const float E = 1.6f;

        static Mapa M;
        static readonly Dictionary<string, Material> materiales = new Dictionary<string, Material>();

        // =====================================================================================
        [MenuItem("PoliRooms/Construir Nivel 1 (Pecera)", priority = 1)]
        public static void Construir()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.Refresh();
            materiales.Clear();
            ReimportarRecursos();
            var controlador = ConstruirControladorAnimacion();

            QuitarVacalluzzoInterno();

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigurarAmbiente();

            // --- Pecera (modelo del equipo) ---
            var prefabPecera = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPecera);
            if (prefabPecera == null) { Error("No encontré " + RutaPecera); return; }
            var pecera = (GameObject)PrefabUtility.InstantiatePrefab(prefabPecera);
            pecera.name = "Pecera";
            pecera.transform.SetPositionAndRotation(Vector3.zero, pecera.transform.rotation);
            AsegurarColliders(pecera);
            OcultarCortadores(pecera.transform);

            M = Mapa.Detectar(pecera.transform);
            if (M == null) { Error("No pude reconocer las piezas de pecera.blend (Plane, pupitres, techo)."); return; }
            Debug.Log($"[PoliRooms] Mapeo Blender→Unity: X→{M.ex}, Y→{M.ey}, Z→{M.ez}, origen {M.o}");

            var escenario = new GameObject("Escenario").transform;
            ConstruirAula(escenario);
            ConstruirPasillo(escenario);
            var gaveta = ConstruirGaveta(escenario);
            var puerta = PrepararPuerta(pecera.transform);
            ConstruirLuces();

            // --- NavMesh (antes de crear personajes y triggers) ---
            HornearNavMesh();

            // --- Puntos de referencia ---
            var puntos = new GameObject("Puntos").transform;
            Vector3 haciaPizarron = M.D(-1, 0, 0);
            Vector3 haciaAlumnos = M.D(1, 0, 0);
            var asiento = PuntoAsiento(pecera.transform, puntos, haciaPizarron);
            var levantarse = Punto(puntos, "PuntoLevantarse", -3.42f, 1.05f, haciaPizarron);
            // Escritorio de Mayra: del lado del pizarrón opuesto a la puerta (está en pecera.blend → Detalles)
            var escritorioMayra = Punto(puntos, "MayraEscritorio", -11.55f, -5.0f, haciaAlumnos);
            var puertaMayra = Punto(puntos, "MayraPuerta", -8.0f, 8.4f, M.D(0, -1, 0));
            var patrulla = new[]
            {
                new Vector2(-9.2f, -4f), new Vector2(-9.2f, 5f), new Vector2(-5.45f, -2.75f), new Vector2(-2f, -2.75f),
                new Vector2(1.8f, -2.75f), new Vector2(-5.45f, 4.9f), new Vector2(-2f, 4.9f), new Vector2(1.8f, 4.9f),
                new Vector2(6.2f, -6f), new Vector2(6.2f, 6f), new Vector2(-2f, -6.4f), new Vector2(-2f, 10.5f), new Vector2(4f, 10.5f)
            }.Select((p, i) => Punto(puntos, "Patrulla" + i, p.x, p.y, haciaAlumnos)).ToArray();

            var rutaEntrada = Ruta(puntos, "GonzaEntrada", new Vector2(-8.5f, 14.2f), new Vector2(-8.5f, 12f), new Vector2(-8.5f, 9.8f),
                                   new Vector2(-9.2f, 1.05f), new Vector2(-3.6f, 1.15f));
            var rutaGaveta = Ruta(puntos, "GonzaGaveta", new Vector2(-9.2f, 1.05f), new Vector2(-9.2f, 2.75f), new Vector2(-11.1f, 2.75f));
            var rutaSalida = Ruta(puntos, "GonzaSalida", new Vector2(-9.2f, 2.0f), new Vector2(-9.2f, -2.75f), new Vector2(-5.45f, -2.75f),
                                  new Vector2(-5.45f, 4.9f), new Vector2(-9.2f, 4.9f), new Vector2(-8.5f, 9.8f), new Vector2(-8.5f, 14.2f));
            var preceptorFinal = Punto(puntos, "PreceptorFinal", 5.4f, 14.5f, M.D(-1, 0, 0));

            // --- Bancos y llave ---
            var bancos = PrepararBancos(pecera.transform);
            var llave = CrearLlave();

            // --- Jugador ---
            var jugador = CrearJugador(levantarse.position, out var camara, out var interaccion, out var flash, out var audioJugador);

            // --- Personajes ---
            var personajes = new GameObject("Personajes").transform;
            CrearSillaMayra(pecera.transform, escritorioMayra);
            var mayra = CrearMayra(personajes, escritorioMayra, puertaMayra, patrulla, asiento, audioJugador, controlador);
            var gonza = CrearPersonaje("Gonza", RutaGonza + ".fbx", RutaGonza + ".png", Color.white, 1.75f * E, GiroGonza,
                                       rutaEntrada[0].position, Quaternion.LookRotation(M.D(0, -1, 0)), personajes,
                                       controlador, AnimadorPersonaje.Locomocion);
            var preceptor = CrearPersonaje("Laura", RutaPreceptor + ".fbx", RutaPreceptor + ".png", Color.white, 1.68f * E, GiroPreceptor,
                                           M.P(13.5f, 14.5f, 0f), Quaternion.LookRotation(M.D(-1, 0, 0)), personajes,
                                           controlador, AnimadorPersonaje.Locomocion);
            AgregarPasos(gonza);
            AgregarPasos(preceptor);

            // --- Sonidos reales ---
            if (puerta != null) puerta.clipAbrir = Audio("PuertaAbrir.mp3");
            gaveta.clipLlaveCandado = Audio("LlaveCerradura.wav");
            gaveta.tiempoAbrir = 2.5f; // lo que dura meter y girar la llave
            llave.clip = Audio("LlaveTintineo.wav");
            jugador.clipPasosLoop = Audio("PasosZapatillasSigilo.wav");

            // --- Post-proceso y efectos ---
            var volumen = CrearPostProceso();
            var efectos = camara.gameObject.AddComponent<EfectosCamara>();
            efectos.volumen = volumen;
            efectos.perseguidor = mayra;
            var estatica = camara.gameObject.AddComponent<AudioSource>();
            estatica.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Poli/Audio/static-fnaf.wav");
            estatica.loop = true; estatica.playOnAwake = true; estatica.volume = 0f; estatica.spatialBlend = 0f;
            efectos.audioEstatica = estatica;

            // --- Trigger del final (en el pasillo, a la derecha) ---
            var zona = new GameObject("ZonaFinal");
            zona.transform.position = M.P(4f, 14.565f, 2.2f);
            var zc = zona.AddComponent<BoxCollider>();
            zc.isTrigger = true;
            zc.size = M.T(1f, 3.9f, 4.4f);
            var rb = zona.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            var zonaTrigger = zona.AddComponent<ZonaTrigger>();

            // --- Sistemas + guion ---
            var sistemas = new GameObject("Sistemas");
            sistemas.AddComponent<GestorJuego>();
            sistemas.AddComponent<Cordura>();
            sistemas.AddComponent<HUD>();
            var audio2D = sistemas.AddComponent<AudioSource>();
            audio2D.playOnAwake = false; audio2D.spatialBlend = 0f;
            var ambiente = new GameObject("Ambiente").AddComponent<AudioSource>();
            ambiente.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Poli/Audio/backroomsLightNoise.wav");
            ambiente.loop = true; ambiente.playOnAwake = true; ambiente.volume = 0.035f; ambiente.spatialBlend = 0f;

            var director = sistemas.AddComponent<DirectorNivel1>();
            director.jugador = jugador;
            director.interaccion = interaccion;
            director.flash = flash;
            director.asientoJugador = asiento;
            director.puntoLevantarse = levantarse;
            director.mayra = mayra;
            director.gonza = gonza;
            director.gonzaRutaEntrada = rutaEntrada;
            director.gonzaRutaGaveta = rutaGaveta;
            director.gonzaRutaSalida = rutaSalida;
            director.preceptor = preceptor;
            director.preceptorPuntoFinal = preceptorFinal;
            director.gaveta = gaveta;
            director.puerta = puerta;
            director.llave = llave;
            director.zonaFinal = zonaTrigger;
            director.audio2D = audio2D;
            director.vozHombre = Audio("VozHombreHmm.wav");
            director.vozMujer = Audio("VozMujerSospecha.wav");
            director.velocidadGonza = 2.4f;

            // --- Guardar ---
            System.IO.Directory.CreateDirectory(CarpetaEscena);
            EditorSceneManager.SaveScene(escena, RutaEscena);
            AgregarABuild(RutaEscena);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("PoliRooms",
                "Nivel 1 construido y guardado en\n" + RutaEscena +
                $"\n\nBancos revisables: {bancos}\nDale Play para probarlo.", "Dale");
        }

        // =====================================================================================
        [MenuItem("PoliRooms/Quitar Vacalluzzo de SampleScene", priority = 20)]
        public static void QuitarVacalluzzo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int n = QuitarVacalluzzoInterno();
            EditorUtility.DisplayDialog("PoliRooms", n > 0 ? $"Eliminé {n} objeto(s) de Vacalluzzo." : "No quedaba nada de Vacalluzzo.", "Ok");
        }

        static int QuitarVacalluzzoInterno()
        {
            if (!System.IO.File.Exists(RutaSampleScene)) return 0;
            var e = EditorSceneManager.OpenScene(RutaSampleScene, OpenSceneMode.Single);
            var objetos = e.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject)
                .Where(g => g.name.ToLowerInvariant().Contains("vacalluz"))
                .ToList();
            int n = 0;
            foreach (var g in objetos)
            {
                if (g == null) continue;
                Object.DestroyImmediate(g);
                n++;
            }
            if (n > 0)
            {
                EditorSceneManager.SaveScene(e);
                Debug.Log($"[PoliRooms] Eliminados {n} objeto(s) de Vacalluzzo de SampleScene.");
            }
            return n;
        }

        // =====================================================================================
        // Ambiente

        static void ConfigurarAmbiente()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.135f, 0.118f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.028f;
            RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.03f);
        }

        /// <summary>Esconde el cilindro que solo sirve para hacer el agujero en la columna celeste.</summary>
        static void OcultarCortadores(Transform pecera)
        {
            foreach (var t in pecera.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Cylinder")) continue;
                foreach (var r in t.GetComponents<Renderer>()) r.enabled = false;
                foreach (var c in t.GetComponents<Collider>()) c.enabled = false;
            }
        }

        static void AsegurarColliders(GameObject raiz)
        {
            foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.GetComponent<Collider>() != null || mf.sharedMesh == null) continue;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        }

        // =====================================================================================
        // Aula: pizarrón, escritorio de Mayra, vidrios de las ventanas

        static void ConstruirAula(Transform padre)
        {
            var aula = new GameObject("AgregadosAula").transform;
            aula.SetParent(padre, false);

            // El pizarrón, el escritorio de Mayra y el resto de la decoración ahora están en
            // pecera.blend (colección "Detalles").

            // Vidrios invisibles en las ventanas (para no salir por ahí)
            Caja(aula, "VidrioVentanaFrente", B(-12.5f, 8f, 2f), M.T(0.25f, 3f, 2f), null, true);
            Caja(aula, "VidrioVentanaPasillo", B(-2f, 12.5f, 2f), M.T(3f, 0.25f, 2f), null, true);
        }

        // =====================================================================================
        // Pasillo de afuera (a la derecha al salir)

        static void ConstruirPasillo(Transform padre)
        {
            var p = new GameObject("Pasillo").transform;
            p.SetParent(padre, false);

            var piso = Mat("PisoPasillo", Color.white, 0.25f);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_PoliRooms/Texturas/SueloCeramico.png");
            if (tex != null) { piso.mainTexture = tex; piso.mainTextureScale = new Vector2(12.75f, 1.6f); }
            else piso.color = new Color(0.78f, 0.72f, 0.62f);
            var pared = Mat("ParedPasillo", new Color(0.9f, 0.9f, 0.87f), 0.1f);
            var zocalo = Mat("ZocaloVerde", new Color(0.42f, 0.64f, 0.55f), 0.2f);
            var techo = Mat("TechoPasillo", new Color(0.82f, 0.82f, 0.8f), 0.05f);
            var maderaPuerta = Mat("PuertaMadera", new Color(0.62f, 0.4f, 0.2f), 0.3f);
            var rojo = Mat("MarcoRojo", new Color(0.62f, 0.1f, 0.08f), 0.3f);
            var rojoMatafuegos = Mat("Matafuegos", new Color(0.8f, 0.08f, 0.05f), 0.6f);
            var amarillo = Mat("TachoAmarillo", new Color(0.85f, 0.85f, 0.15f), 0.3f);
            var negro = Mat("TachoNegro", new Color(0.08f, 0.08f, 0.08f), 0.3f);

            const float H = 4.5f; // alto del pasillo (escala del aula)
            Caja(p, "Piso", B(2.69f, 14.565f, -0.05f), M.T(30.62f, 3.87f, 0.1f), piso, true);
            Caja(p, "Techo", B(2.69f, 14.565f, H + 0.05f), M.T(30.62f, 3.87f, 0.1f), techo, true);
            Caja(p, "ParedNorte", B(2.69f, 16.6f, H / 2), M.T(30.62f, 0.2f, H), pared, true);
            Caja(p, "ParedOeste", B(-12.75f, 14.565f, H / 2), M.T(0.2f, 4.1f, H), pared, true);
            Caja(p, "ParedEste", B(18.1f, 14.565f, H / 2), M.T(0.2f, 4.1f, H), pared, true);
            Caja(p, "ParedSurEste", B(12.86f, 12.6f, H / 2), M.T(10.5f, 0.2f, H), pared, true);
            Caja(p, "ZocaloNorte", B(2.69f, 16.49f, 0.95f), M.T(30.6f, 0.02f, 1.9f), zocalo, false);
            Caja(p, "ZocaloSur", B(5.75f, 12.74f, 0.95f), M.T(24.5f, 0.02f, 1.9f), zocalo, false);

            // Puertas de otras aulas (cerradas) con marco rojo, como en las fotos
            foreach (float x in new[] { -2f, 6.5f, 13.5f })
            {
                Caja(p, "MarcoPuerta", B(x, 16.48f, 1.72f), M.T(1.76f, 0.05f, 3.44f), rojo, false);
                Caja(p, "PuertaCerrada", B(x, 16.45f, 1.64f), M.T(1.52f, 0.05f, 3.28f), maderaPuerta, false);
            }
            Caja(p, "PuertaFondo", B(18.0f, 14.565f, 1.64f), M.T(0.05f, 1.52f, 3.28f), maderaPuerta, false);

            // Matafuegos y tachos
            Cilindro(p, "Matafuegos", B(9.5f, 16.33f, 2.0f), 0.27f, 0.8f, rojoMatafuegos);
            Caja(p, "CartelMatafuegos", B(9.5f, 16.48f, 2.7f), M.T(0.56f, 0.02f, 0.72f), rojo, false);
            Cilindro(p, "TachoAmarillo", B(2.2f, 16.15f, 0.5f), 0.64f, 1.0f, amarillo);
            Cilindro(p, "TachoNegro", B(3.0f, 16.15f, 0.5f), 0.64f, 1.0f, negro);
        }

        // =====================================================================================
        // Gaveta de celulares (según las fotos): maletín de aluminio con 48 ranuras y candado

        static Gaveta ConstruirGaveta(Transform padre)
        {
            var aluminio = Mat("Aluminio", new Color(0.75f, 0.76f, 0.78f), 0.75f, 0.8f);
            var espuma = Mat("Espuma", new Color(0.95f, 0.95f, 0.96f), 0.05f);
            var ranura = Mat("Ranura", new Color(0.55f, 0.56f, 0.58f), 0.05f);
            var etiqueta = Mat("EtiquetaRoja", new Color(0.7f, 0.12f, 0.12f), 0.1f);
            var acrilico = Mat("Acrilico", new Color(0.85f, 0.9f, 0.95f, 0.25f), 0.9f, 0f, true);
            var bronce = Mat("Bronce", new Color(0.78f, 0.6f, 0.25f), 0.7f, 0.8f);
            var negro = Mat("CelularNegro", new Color(0.04f, 0.04f, 0.05f), 0.7f);
            var monitor = Mat("Monitor", new Color(0.03f, 0.03f, 0.035f), 0.8f);

            var go = new GameObject("Gaveta");
            go.transform.SetParent(padre, false);
            // A la derecha del pizarrón (pizarrón en pecera.blend: y de -2.9 a 1.9), a la altura de los ojos.
            go.transform.position = B(-12.25f, 2.75f, 2.55f);
            go.transform.rotation = Quaternion.LookRotation(M.D(1, 0, 0), Vector3.up); // el frente mira al aula
            go.transform.localScale = Vector3.one * E;
            var t = go.transform;

            // Cuerpo (caja abierta adelante)
            CajaLocal(t, "Fondo", new Vector3(0, 0, -0.075f), new Vector3(0.45f, 0.6f, 0.01f), aluminio);
            CajaLocal(t, "LadoIzq", new Vector3(-0.22f, 0, 0), new Vector3(0.01f, 0.6f, 0.16f), aluminio);
            CajaLocal(t, "LadoDer", new Vector3(0.22f, 0, 0), new Vector3(0.01f, 0.6f, 0.16f), aluminio);
            CajaLocal(t, "Arriba", new Vector3(0, 0.295f, 0), new Vector3(0.45f, 0.01f, 0.16f), aluminio);
            CajaLocal(t, "Abajo", new Vector3(0, -0.295f, 0), new Vector3(0.45f, 0.01f, 0.16f), aluminio);
            CajaLocal(t, "Espuma", new Vector3(0, 0, -0.065f), new Vector3(0.42f, 0.57f, 0.01f), espuma);
            for (int fila = 0; fila < 4; fila++)
            {
                float y = 0.2f - fila * 0.135f;
                for (int col = 0; col < 12; col++)
                    CajaLocal(t, "Ranura", new Vector3(-0.17f + col * 0.031f, y + 0.015f, -0.0595f), new Vector3(0.012f, 0.1f, 0.002f), ranura);
                CajaLocal(t, "Etiquetas", new Vector3(0, y - 0.05f, -0.059f), new Vector3(0.39f, 0.014f, 0.002f), etiqueta);
            }
            // Manija y candado arriba
            CajaLocal(t, "Manija", new Vector3(0, 0.32f, 0), new Vector3(0.16f, 0.02f, 0.025f), aluminio);
            var candado = new GameObject("Candado").transform;
            candado.SetParent(t, false);
            candado.localPosition = new Vector3(-0.09f, 0.31f, 0.03f);
            CajaLocal(candado, "Cuerpo", Vector3.zero, new Vector3(0.045f, 0.04f, 0.02f), bronce);
            CajaLocal(candado, "Arco", new Vector3(0, 0.03f, 0), new Vector3(0.03f, 0.025f, 0.006f), aluminio);

            // Celular guardado en una ranura
            var celular = CajaLocal(t, "CelularAdentro", new Vector3(0.05f, 0.08f, -0.05f), new Vector3(0.035f, 0.14f, 0.01f), negro);

            // Tapa de acrílico con bisagra a la derecha (vista de frente)
            var bisagra = new GameObject("BisagraTapa").transform;
            bisagra.SetParent(t, false);
            bisagra.localPosition = new Vector3(-0.225f, 0f, 0.08f);
            CajaLocal(bisagra, "Tapa", new Vector3(0.225f, 0, 0), new Vector3(0.45f, 0.6f, 0.006f), acrilico);
            CajaLocal(bisagra, "MarcoTapaArriba", new Vector3(0.225f, 0.295f, 0.004f), new Vector3(0.45f, 0.012f, 0.012f), aluminio);
            CajaLocal(bisagra, "MarcoTapaAbajo", new Vector3(0.225f, -0.295f, 0.004f), new Vector3(0.45f, 0.012f, 0.012f), aluminio);

            // Monitor con brazo abajo (como en la foto)
            Caja(padre, "MonitorGaveta", B(-12.31f, 2.75f, 1.6f), M.T(0.08f, 0.88f, 0.53f), monitor, false);

            var colGaveta = go.AddComponent<BoxCollider>();
            colGaveta.size = new Vector3(0.47f, 0.66f, 0.2f);
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.spatialBlend = 1f; audio.maxDistance = 15f;

            var g = go.AddComponent<Gaveta>();
            g.bisagraTapa = bisagra;
            g.anguloAbierta = -100f;
            g.candado = candado.gameObject;
            g.celularAdentro = celular;
            g.fuenteAudio = audio;
            return g;
        }

        // =====================================================================================
        // Puerta del aula: se le arma una bisagra para poder abrirla

        static PuertaAula PrepararPuerta(Transform pecera)
        {
            var original = BuscarQueEmpiece(pecera, "Meshy_AI_low_poly_wooden_door");
            if (original == null) { Debug.LogError("[PoliRooms] No encontré la puerta en pecera.blend"); return null; }

            Vector3 bisagraPos = B(-9.48f, 11f, 0f);
            var bisagra = new GameObject("PuertaAula").transform;
            bisagra.position = bisagraPos;
            bisagra.rotation = Quaternion.identity;

            // No se puede reacomodar un hijo del prefab de pecera.blend (y no queremos desempaquetarlo,
            // así los cambios del .blend siguen llegando). Copiamos la puerta y escondemos la original.
            var puertaObj = Object.Instantiate(original.gameObject, original.position, original.rotation).transform;
            puertaObj.name = "HojaPuerta";
            puertaObj.localScale = original.lossyScale;
            puertaObj.SetParent(bisagra, true);
            original.gameObject.SetActive(false);
            if (puertaObj.GetComponentInChildren<Collider>() == null)
            {
                var r = puertaObj.GetComponentInChildren<Renderer>();
                if (r != null) r.gameObject.AddComponent<BoxCollider>();
            }

            // Gira hacia adentro del aula
            Bounds b = Limites(puertaObj.gameObject);
            Vector3 hoja = b.center - bisagraPos; hoja.y = 0f;
            Vector3 haciaAula = B(-2.5f, 1.75f, 0f) - bisagraPos; haciaAula.y = 0f;
            float angulo = Vector3.Dot(Quaternion.Euler(0, 95f, 0) * hoja, haciaAula) > 0f ? 95f : -95f;

            var audio = bisagra.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.spatialBlend = 1f; audio.maxDistance = 20f;
            var p = bisagra.gameObject.AddComponent<PuertaAula>();
            p.bisagra = bisagra;
            p.anguloAbierta = angulo;
            p.fuenteAudio = audio;

            // PuertaAula gira la bisagra; para que su rotación local arranque en 0, la ponemos dentro de un padre fijo.
            var soporte = new GameObject("SoportePuerta").transform;
            soporte.position = bisagraPos;
            bisagra.SetParent(soporte, true);
            return p;
        }

        // =====================================================================================
        // Luces

        static void ConstruirLuces()
        {
            var luces = new GameObject("Luces").transform;
            // Más oscuras y más cálidas (tubos viejos amarillentos)
            var calida = new Color(1f, 0.8f, 0.58f);
            var calidaSucia = new Color(1f, 0.72f, 0.48f);
            // Tubos del aula (mismas posiciones que en el .blend)
            Luz(luces, "LuzAula1", B(-6.4f, 6.3f, 5.6f), calida, 0.85f, 14f, LightShadows.Soft, false);
            Luz(luces, "LuzAula2", B(1.0f, -3.6f, 4.6f), calida, 0.85f, 14f, LightShadows.Soft, false);
            Luz(luces, "LuzAula3", B(-6.4f, -6.1f, 5.6f), calidaSucia, 0.72f, 14f, LightShadows.Soft, true);
            Luz(luces, "LuzAula4", B(1.0f, 6.3f, 4.6f), calida, 0.85f, 14f, LightShadows.Soft, false);
            Luz(luces, "LuzFrente", B(-10f, -1.5f, 4.2f), calidaSucia, 0.45f, 9f, LightShadows.None, false);
            // Pasillo
            Luz(luces, "LuzPasillo1", B(-8f, 14.5f, 4.2f), calida, 0.66f, 9f, LightShadows.Soft, false);
            Luz(luces, "LuzPasillo2", B(2f, 14.5f, 4.2f), calidaSucia, 0.6f, 9f, LightShadows.None, true);
            Luz(luces, "LuzPasillo3", B(10f, 14.5f, 4.2f), calida, 0.66f, 9f, LightShadows.Soft, false);
            Luz(luces, "LuzPasillo4", B(16f, 14.5f, 4.2f), calidaSucia, 0.5f, 9f, LightShadows.None, true);
        }

        static void Luz(Transform padre, string nombre, Vector3 pos, Color color, float intensidad, float rango, LightShadows sombras, bool parpadea)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensidad;
            l.range = rango;
            l.shadows = sombras;
            if (parpadea) go.AddComponent<LuzParpadeante>();
        }

        // =====================================================================================
        // NavMesh

        static void HornearNavMesh()
        {
            var go = new GameObject("Navegacion");
            var s = go.AddComponent<NavMeshSurface>();
            s.collectObjects = CollectObjects.All;
            s.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            s.layerMask = ~(1 << 2);
            s.BuildNavMesh();
            if (s.navMeshData == null) { Debug.LogError("[PoliRooms] No se pudo hornear el NavMesh."); return; }
            System.IO.Directory.CreateDirectory(CarpetaEscena);
            string ruta = CarpetaEscena + "/N1_NavMesh.asset";
            AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(s.navMeshData, ruta);
        }

        // =====================================================================================
        // Bancos y llave

        static int PrepararBancos(Transform pecera)
        {
            var pupitres = pecera.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("pupitre_escolar_lowpoly") && t.GetComponent<Renderer>() != null)
                .ToList();
            Vector3 miBanco = B(-4.34f, -0.29f, 0.6f);
            BancoRevisable masCercano = null;
            float mejor = float.MaxValue;
            foreach (var t in pupitres)
            {
                if (t.GetComponent<Collider>() == null) t.gameObject.AddComponent<BoxCollider>();
                var b = t.gameObject.AddComponent<BancoRevisable>();
                float d = Vector3.Distance(Limites(t.gameObject).center, miBanco);
                if (d < mejor) { mejor = d; masCercano = b; }
            }
            if (masCercano != null) masCercano.esBancoDelJugador = true;
            return pupitres.Count;
        }

        static LlaveEscondida CrearLlave()
        {
            var go = new GameObject("LlaveCandado");
            var bronce = Mat("Bronce", new Color(0.78f, 0.6f, 0.25f), 0.7f, 0.8f);
            CajaLocal(go.transform, "Llave", Vector3.zero, new Vector3(0.06f, 0.012f, 0.022f), bronce);
            CajaLocal(go.transform, "Cinta", new Vector3(0, 0.008f, 0), new Vector3(0.05f, 0.002f, 0.04f), Mat("Cinta", new Color(0.8f, 0.78f, 0.7f), 0.1f));
            go.AddComponent<AudioSource>();
            return go.AddComponent<LlaveEscondida>();
        }

        // =====================================================================================
        // Jugador

        static ControladorJugador CrearJugador(Vector3 pos, out Camera camara, out InteraccionJugador interaccion,
                                               out FlashCelular flash, out AudioSource audio)
        {
            var go = new GameObject("Jugador");
            go.tag = "Player";
            go.layer = 2; // Ignore Raycast: no bloquea la vista de Mayra ni los raycasts de interacción
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(M.D(-1, 0, 0));

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f * E; cc.radius = 0.3f * E; cc.center = new Vector3(0, 0.9f * E, 0);
            cc.stepOffset = 0.3f * E; cc.slopeLimit = 45f; cc.skinWidth = 0.05f;

            var camGo = new GameObject("Camara");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(go.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.6f * E, 0);
            camara = camGo.AddComponent<Camera>();
            camara.nearClipPlane = 0.03f;
            camara.fieldOfView = 70f;
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();

            var ppl = camGo.AddComponent<PostProcessLayer>();
            var res = AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
            ppl.Init(res);
            ppl.volumeTrigger = camGo.transform;
            ppl.volumeLayer = 1; // Default
            ppl.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;

            var luzGo = new GameObject("LuzFlash");
            luzGo.transform.SetParent(camGo.transform, false);
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Spot;
            luz.enabled = false;
            luz.shadows = LightShadows.None;

            var ctrl = go.AddComponent<ControladorJugador>();
            ctrl.camara = camGo.transform;
            var pasos = go.AddComponent<AudioSource>();
            pasos.playOnAwake = false; pasos.spatialBlend = 0f;
            ctrl.fuentePasos = pasos;
            go.AddComponent<SistemaRuido>();
            go.AddComponent<Inventario>();
            interaccion = go.AddComponent<InteraccionJugador>();
            audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.spatialBlend = 0f;
            flash = go.AddComponent<FlashCelular>();
            flash.luz = luz;
            flash.fuenteAudio = audio;
            return ctrl;
        }

        // =====================================================================================
        // Personajes

        static ProfesoraMayra CrearMayra(Transform padre, Transform escritorio, Transform puerta, Transform[] patrulla,
                                         Transform asiento, AudioSource audioJugador, RuntimeAnimatorController controlador)
        {
            var raiz = CrearPersonaje("Mayra", RutaMayra + ".fbx", RutaMayra + ".png", Color.white, 1.68f * E, GiroMayra,
                                      escritorio.position, escritorio.rotation, padre, controlador, AnimadorPersonaje.Sentado);
            raiz.gameObject.layer = 2;

            var ag = raiz.gameObject.AddComponent<NavMeshAgent>();
            ag.radius = 0.5f; ag.height = 1.7f * E; ag.speed = 3.7f;
            ag.angularSpeed = 400f; ag.acceleration = 20f; ag.stoppingDistance = 0.4f;
            ag.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var pasos = new GameObject("Pasos").AddComponent<AudioSource>();
            pasos.transform.SetParent(raiz, false);
            pasos.clip = Audio("PasosTacos.wav") ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bicho/Audio/pasos-reverb.wav");
            pasos.loop = true; pasos.playOnAwake = false; pasos.spatialBlend = 1f; pasos.maxDistance = 25f; pasos.volume = 0.3f;
            pasos.rolloffMode = AudioRolloffMode.Linear;
            var voz = new GameObject("Voz").AddComponent<AudioSource>();
            voz.transform.SetParent(raiz, false);
            voz.transform.localPosition = Vector3.up * 2.4f;
            voz.playOnAwake = false; voz.spatialBlend = 1f; voz.maxDistance = 30f; voz.rolloffMode = AudioRolloffMode.Linear;
            var susto = raiz.gameObject.AddComponent<AudioSource>();
            susto.playOnAwake = false; susto.spatialBlend = 0f;
            var lapicera = new GameObject("Lapicera").AddComponent<AudioSource>();
            lapicera.transform.SetParent(raiz, false);
            lapicera.transform.localPosition = Vector3.up * 1.2f;
            lapicera.playOnAwake = false; lapicera.spatialBlend = 1f; lapicera.rolloffMode = AudioRolloffMode.Linear;
            lapicera.minDistance = 1f; lapicera.maxDistance = 14f;

            var m = raiz.gameObject.AddComponent<ProfesoraMayra>();
            m.visual = raiz.Find("Visual");
            m.puntoEscritorio = escritorio;
            m.puntoPuerta = puerta;
            m.puntosPatrulla = patrulla;
            m.asientoJugador = asiento;
            m.radioAsiento = 1.45f;
            m.audioPasos = pasos;
            m.audioSusto = susto;
            m.audioLapicera = lapicera;
            m.audioVoz = voz;
            m.clipSospecha = Audio("VozMujerSospecha.wav");
            m.animador = raiz.GetComponent<AnimadorPersonaje>();
            m.clipSusto = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bicho/Audio/pianito-dross.wav");
            m.audioDeteccion = audioJugador;
            m.clipDeteccion = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bicho/Audio/Child-gasp-sfx.wav");
            return m;
        }

        /// <summary>Crea Raíz → Visual → modelo (escalado a la altura pedida, con los pies en el piso).</summary>
        static Transform CrearPersonaje(string nombre, string rutaModelo, string rutaTextura, Color tinte, float altura, float giroY,
                                        Vector3 pos, Quaternion rot, Transform padre, RuntimeAnimatorController controlador, int accionInicial)
        {
            var raiz = new GameObject(nombre).transform;
            raiz.SetParent(padre, false);
            raiz.SetPositionAndRotation(pos, rot);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(raiz, false);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaModelo);
            if (prefab == null) { Debug.LogError("[PoliRooms] No encontré el modelo " + rutaModelo); return raiz; }
            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            modelo.transform.SetParent(visual, false);
            modelo.transform.localPosition = Vector3.zero;
            modelo.transform.localRotation = Quaternion.Euler(0f, giroY, 0f);
            modelo.transform.localScale = Vector3.one;

            Bounds b = Limites(modelo);
            if (b.size.y > 0.001f) modelo.transform.localScale = Vector3.one * (altura / b.size.y);
            var anim0 = modelo.GetComponentInChildren<Animator>();
            if (anim0 != null && anim0.avatar != null && anim0.avatar.isHuman)
            {
                // Humanoide: el Animator apoya los pies sobre su propio origen (animaciones "Based upon Feet").
                // Los bounds del skinned mesh en el editor no son confiables para la altura, así que no se usan.
                modelo.transform.localPosition = Vector3.zero;
            }
            else
            {
                b = Limites(modelo);
                modelo.transform.position += raiz.position - new Vector3(b.center.x, b.min.y, b.center.z);
            }

            foreach (var c in modelo.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            // Material con la textura del modelo (por si el .mtl no se importó bien)
            var tex = rutaTextura != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTextura) : null;
            if (tex != null)
            {
                var mat = Mat("Personaje_" + nombre, tinte, 0.05f);
                mat.mainTexture = tex;
                foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            }

            // Animación: humanoide → Animator con las animaciones de Mixamo; sin esqueleto → procedural.
            var animator = modelo.GetComponentInChildren<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isHuman && controlador != null)
            {
                animator.runtimeAnimatorController = controlador;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var ap = raiz.gameObject.AddComponent<AnimadorPersonaje>();
                ap.animator = animator;
                ap.accionInicial = accionInicial;
            }
            else
            {
                if (animator != null) Object.DestroyImmediate(animator);
                var anim = modelo.AddComponent<AnimacionProcedural>();
                anim.raizMovimiento = raiz;
                anim.altura = altura;
            }
            return raiz;
        }

        static void AgregarPasos(Transform personaje)
        {
            personaje.gameObject.AddComponent<AudioSource>();
            var p = personaje.gameObject.AddComponent<PasosPersonaje>();
            p.clipLoop = Audio("PasosZapatillas.wav");
        }

        static AudioClip Audio(string archivo)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(CarpetaAudio + archivo);
            if (c == null) Debug.LogWarning("[PoliRooms] Falta el audio " + CarpetaAudio + archivo);
            return c;
        }

        /// <summary>Fuerza a que animaciones y audios se importen con la configuración de ImportadorPoliRooms.</summary>
        static void ReimportarRecursos()
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { CarpetaAnim.TrimEnd('/'), CarpetaAudio.TrimEnd('/'), CarpetaRiggeados.TrimEnd('/') }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (ruta.EndsWith(".fbx") || ruta.EndsWith(".wav") || ruta.EndsWith(".mp3"))
                    AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
            }
        }

        static AnimationClip Clip(string ruta)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) Debug.LogError("[PoliRooms] No encontré animación en " + ruta);
            return clip;
        }

        /// <summary>
        /// Arma el Animator Controller compartido por los personajes. Parámetros:
        /// "Velocidad" (float, m/s) y "Accion" (int, ver constantes de AnimadorPersonaje).
        /// </summary>
        static RuntimeAnimatorController ConstruirControladorAnimacion()
        {
            AssetDatabase.DeleteAsset(RutaControladorAnim);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(RutaControladorAnim);
            ctrl.AddParameter("Velocidad", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Accion", AnimatorControllerParameterType.Int);
            var sm = ctrl.layers[0].stateMachine;

            var idle = Clip(RutaIdle);
            var caminar = Clip(CarpetaAnim + "Caminar.fbx");
            var sigilo = Clip(CarpetaAnim + "CaminarSigilo.fbx");
            var pesado = Clip(CarpetaAnim + "CaminarPesado.fbx");
            var agachado = Clip(CarpetaAnim + "CaminarAgachado.fbx");

            // Locomoción: quieto → caminar → caminar rápido (el mismo clip acelerado).
            UnityEditor.Animations.BlendTree Locomocion(string nombre, AnimationClip clip, float velNormal)
            {
                var bt = new UnityEditor.Animations.BlendTree
                {
                    name = nombre,
                    blendType = UnityEditor.Animations.BlendTreeType.Simple1D,
                    blendParameter = "Velocidad",
                    useAutomaticThresholds = false,
                    hideFlags = HideFlags.HideInHierarchy
                };
                AssetDatabase.AddObjectToAsset(bt, ctrl);
                bt.AddChild(idle, 0f);
                bt.AddChild(clip, velNormal);
                bt.AddChild(clip, velNormal * 2.5f);
                var hijos = bt.children;
                hijos[2].timeScale = 2.2f;
                bt.children = hijos;
                return bt;
            }

            void Estado(string nombre, Motion motion, int accion, bool porDefecto = false)
            {
                var st = sm.AddState(nombre);
                st.motion = motion;
                if (porDefecto) sm.defaultState = st;
                var tr = sm.AddAnyStateTransition(st);
                tr.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, accion, "Accion");
                tr.hasExitTime = false;
                tr.duration = 0.25f;
                tr.canTransitionToSelf = false;
            }

            Estado("Locomocion", Locomocion("ArbolCaminar", caminar, 2.2f), AnimadorPersonaje.Locomocion, true);
            Estado("Sigilo", Locomocion("ArbolSigilo", sigilo, 1.6f), AnimadorPersonaje.Sigilo);
            Estado("Escribiendo", Clip(CarpetaAnim + "Escribiendo.fbx"), AnimadorPersonaje.Escribiendo);
            Estado("SentadaMirando", Clip(CarpetaAnim + "SentadoDesaprobacion.fbx"), AnimadorPersonaje.SentadaMirando);
            Estado("Levantarse", Clip(CarpetaAnim + "Levantarse.fbx"), AnimadorPersonaje.Levantarse);
            Estado("Pesado", Locomocion("ArbolPesado", pesado, 2.2f), AnimadorPersonaje.Pesado);
            Estado("Sentado", Clip(CarpetaAnim + "Sentado.fbx"), AnimadorPersonaje.Sentado);
            Estado("Agachado", Locomocion("ArbolAgachado", agachado, 1.4f), AnimadorPersonaje.Agachado);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        /// <summary>Una silla del aula (copiada del .blend) para que Mayra se siente detrás del escritorio.</summary>
        static void CrearSillaMayra(Transform pecera, Transform puntoMayra)
        {
            var original = pecera.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(r => r.name.StartsWith("Meshy_AI_Low_Poly_School_Chair"));
            if (original == null) return;
            var silla = Object.Instantiate(original.gameObject);
            silla.name = "SillaMayra";
            silla.transform.localScale = original.transform.lossyScale;
            // Las sillas de los alumnos miran al pizarrón; la de Mayra mira al aula (girada 180°).
            silla.transform.rotation = Quaternion.AngleAxis(180f, Vector3.up) * original.transform.rotation;
            Bounds b = silla.GetComponent<Renderer>().bounds;
            silla.transform.position += new Vector3(puntoMayra.position.x - b.center.x, puntoMayra.position.y - b.min.y, puntoMayra.position.z - b.center.z);
        }

        /// <summary>
        /// Punto donde se sienta el jugador: el centro real de su silla (sin pasar por el NavMesh,
        /// que lo corría fuera de la silla).
        /// </summary>
        static Transform PuntoAsiento(Transform pecera, Transform padre, Vector3 mirar)
        {
            Vector3 aprox = B(-3.42f, -0.31f, 0f);
            var silla = pecera.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.StartsWith("Meshy_AI_Low_Poly_School_Chair"))
                .OrderBy(r => Vector3.Distance(Plano(r.bounds.center), Plano(aprox)))
                .FirstOrDefault();
            Vector3 p = aprox;
            if (silla != null)
            {
                Vector3 c = silla.bounds.center;
                p = new Vector3(c.x, aprox.y, c.z);
                // Tu silla: E para sentarte de nuevo (y que Mayra deje de buscarte).
                if (silla.GetComponent<Collider>() == null) silla.gameObject.AddComponent<MeshCollider>();
                silla.gameObject.AddComponent<AsientoPropio>();
            }
            var t = new GameObject("AsientoJugador").transform;
            t.SetParent(padre, false);
            t.position = p;
            t.rotation = Quaternion.LookRotation(new Vector3(mirar.x, 0f, mirar.z).normalized, Vector3.up);
            return t;
        }

        static Vector3 Plano(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // =====================================================================================
        // Post-proceso

        static PostProcessVolume CrearPostProceso()
        {
            System.IO.Directory.CreateDirectory(CarpetaEscena);
            string ruta = CarpetaEscena + "/N1_PostProceso.asset";
            AssetDatabase.DeleteAsset(ruta);
            var perfil = ScriptableObject.CreateInstance<PostProcessProfile>();
            AssetDatabase.CreateAsset(perfil, ruta);

            T Agregar<T>() where T : PostProcessEffectSettings
            {
                var s = perfil.AddSettings<T>();
                s.name = typeof(T).Name;
                s.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(s, perfil);
                return s;
            }

            var vineta = Agregar<Vignette>();
            vineta.enabled.Override(true);
            vineta.intensity.Override(0.3f);
            vineta.smoothness.Override(0.45f);

            var grano = Agregar<Grain>();
            grano.enabled.Override(true);
            grano.intensity.Override(0.2f);

            var ca = Agregar<ChromaticAberration>();
            ca.enabled.Override(true);
            ca.intensity.Override(0f);

            var ld = Agregar<LensDistortion>();
            ld.enabled.Override(true);
            ld.intensity.Override(0f);

            var dof = Agregar<DepthOfField>();
            dof.enabled.Override(false);

            var cg = Agregar<ColorGrading>();
            cg.enabled.Override(true);
            cg.tonemapper.Override(Tonemapper.ACES);
            cg.saturation.Override(-15f);
            cg.contrast.Override(8f);
            cg.temperature.Override(-6f);

            var ao = Agregar<AmbientOcclusion>();
            ao.enabled.Override(true);
            ao.intensity.Override(0.6f);

            EditorUtility.SetDirty(perfil);
            AssetDatabase.SaveAssets();

            var go = new GameObject("PostProceso");
            go.layer = 0;
            var v = go.AddComponent<PostProcessVolume>();
            v.isGlobal = true;
            v.priority = 1f;
            v.sharedProfile = perfil;
            return v;
        }

        // =====================================================================================
        // Build settings

        static void AgregarABuild(string ruta)
        {
            var lista = EditorBuildSettings.scenes.Where(s => s.path != ruta).ToList();
            lista.Insert(0, new EditorBuildSettingsScene(ruta, true));
            EditorBuildSettings.scenes = lista.ToArray();
        }

        // =====================================================================================
        // Helpers

        static Vector3 B(float x, float y, float z) => M.P(x, y, z);

        static Transform Punto(Transform padre, string nombre, float x, float y, Vector3 mirar)
        {
            var t = new GameObject(nombre).transform;
            t.SetParent(padre, false);
            Vector3 p = M.P(x, y, 0f);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) p = hit.position;
            t.position = p;
            t.rotation = Quaternion.LookRotation(new Vector3(mirar.x, 0f, mirar.z).normalized, Vector3.up);
            return t;
        }

        static Transform[] Ruta(Transform padre, string nombre, params Vector2[] pts)
        {
            var grupo = new GameObject(nombre).transform;
            grupo.SetParent(padre, false);
            return pts.Select((p, i) =>
            {
                var t = new GameObject(nombre + i).transform;
                t.SetParent(grupo, false);
                t.position = M.P(p.x, p.y, 0f);
                return t;
            }).ToArray();
        }

        static GameObject Caja(Transform padre, string nombre, Vector3 pos, Vector3 tam, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            go.transform.localScale = tam;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            else Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            go.isStatic = true;
            return go;
        }

        static GameObject CajaLocal(Transform padre, string nombre, Vector3 posLocal, Vector3 escala, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(padre, false);
            go.transform.localPosition = posLocal;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = escala;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Cilindro(Transform padre, string nombre, Vector3 centro, float diametro, float alto, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = nombre;
            go.transform.SetParent(padre, false);
            go.transform.position = centro;
            go.transform.localScale = new Vector3(diametro, alto * 0.5f, diametro);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Material Mat(string nombre, Color color, float brillo, float metal = 0f, bool transparente = false)
        {
            if (materiales.TryGetValue(nombre, out var existente)) return existente;
            System.IO.Directory.CreateDirectory(CarpetaMateriales);
            string ruta = $"{CarpetaMateriales}/{nombre}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, ruta);
            }
            m.color = color;
            m.SetFloat("_Glossiness", brillo);
            m.SetFloat("_Metallic", metal);
            if (transparente)
            {
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)BlendMode.One);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHABLEND_ON");
                m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
            }
            EditorUtility.SetDirty(m);
            materiales[nombre] = m;
            return m;
        }

        static Bounds Limites(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static Transform BuscarQueEmpiece(Transform raiz, string prefijo) =>
            raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(prefijo));

        static void Error(string msg)
        {
            Debug.LogError("[PoliRooms] " + msg);
            EditorUtility.DisplayDialog("PoliRooms", msg, "Ok");
        }

        // =====================================================================================
        /// <summary>
        /// Conversión de coordenadas de Blender (como figuran en pecera.blend) a Unity.
        /// Se detecta comparando dónde quedaron en Unity piezas conocidas del modelo.
        /// </summary>
        class Mapa
        {
            public Vector3 o, ex, ey, ez;

            public Vector3 P(float x, float y, float z) => o + ex * x + ey * y + ez * z;
            public Vector3 D(float x, float y, float z) => (ex * x + ey * y + ez * z).normalized;
            public Vector3 T(float x, float y, float z) => Abs(ex * x) + Abs(ey * y) + Abs(ez * z);
            static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

            static readonly (string nombre, Vector3 b)[] Referencias =
            {
                ("Plane", new Vector3(0f, 0f, 0f)),
                ("pupitre_escolar_lowpoly", new Vector3(-7.52f, 3.66f, 0.605f)),
                ("pupitre_escolar_lowpoly.039", new Vector3(3.31f, 4.90f, 0.605f)),
                ("pupitre_escolar_lowpoly.006", new Vector3(-6.655f, -8.69f, 0.605f)),
                ("pupitre_escolar_lowpoly.030", new Vector3(4.18f, 8.65f, 0.605f)),
                ("techo", new Vector3(-1.5f, 0f, 6.0f)),
                ("Cube__9_", new Vector3(7.5f, 0f, 2f)),
            };

            public static Mapa Detectar(Transform raiz)
            {
                var pares = new List<(Vector3 b, Vector3 u)>();
                foreach (var (nombre, b) in Referencias)
                {
                    var t = Buscar(raiz, nombre);
                    if (t == null) { Debug.LogWarning("[PoliRooms] Referencia no encontrada: " + nombre); continue; }
                    pares.Add((b, Limites(t.gameObject).center));
                }
                if (pares.Count < 4 || pares[0].b != Vector3.zero) return null;

                Vector3 o = pares[0].u;
                var c = new float[3, 3];
                float sumB = 0f, sumU = 0f;
                for (int i = 1; i < pares.Count; i++)
                {
                    Vector3 db = pares[i].b, du = pares[i].u - o;
                    for (int a = 0; a < 3; a++)
                        for (int u = 0; u < 3; u++)
                            c[a, u] += db[a] * du[u];
                    sumB += db.sqrMagnitude;
                    sumU += du.sqrMagnitude;
                }
                float escala = Mathf.Sqrt(sumU / Mathf.Max(1e-6f, sumB));

                var ejes = new Vector3[3];
                var usadoA = new bool[3];
                var usadoU = new bool[3];
                for (int k = 0; k < 3; k++)
                {
                    int ma = -1, mu = -1; float mejor = -1f;
                    for (int a = 0; a < 3; a++)
                        for (int u = 0; u < 3; u++)
                            if (!usadoA[a] && !usadoU[u] && Mathf.Abs(c[a, u]) > mejor) { mejor = Mathf.Abs(c[a, u]); ma = a; mu = u; }
                    usadoA[ma] = usadoU[mu] = true;
                    var e = Vector3.zero;
                    e[mu] = Mathf.Sign(c[ma, mu]) * escala;
                    ejes[ma] = e;
                }
                return new Mapa { o = o, ex = ejes[0], ey = ejes[1], ez = ejes[2] };
            }

            static Transform Buscar(Transform raiz, string nombre)
            {
                var todos = raiz.GetComponentsInChildren<Transform>(true);
                return todos.FirstOrDefault(t => t.name == nombre)
                    ?? todos.FirstOrDefault(t => t.name == nombre.Replace('.', '_'))
                    ?? todos.FirstOrDefault(t => t.name == nombre.Replace(".", ""));
            }
        }
    }
}
