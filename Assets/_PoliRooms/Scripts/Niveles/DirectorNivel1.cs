using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace PoliRooms
{
    /// <summary>
    /// Guion del Nivel 1 (La Pecera):
    /// 1) Gonza te pide el celular, lo guarda en la gaveta con candado y esconde la llave
    ///    abajo de un banco (se ve dónde la deja).
    /// 2) Mayra corrige pruebas; te levantás en sigilo y buscás la llave. Si te busca,
    ///    podés volver a tu banco (E) y hacerte el que estudia.
    /// 3) Abrís la gaveta, recuperás tu celular; Mayra se planta en la puerta.
    /// 4) La encandilás con el flash, salís al pasillo y doblás a la derecha.
    /// 5) Laura, la preceptora, te agarra: "¿Qué hacés afuera del aula, pibe?" — piña — Nivel 2.
    /// </summary>
    public class DirectorNivel1 : MonoBehaviour
    {
        public static DirectorNivel1 Instancia { get; private set; }

        [Header("Jugador")]
        public ControladorJugador jugador;
        public InteraccionJugador interaccion;
        public FlashCelular flash;
        public Transform asientoJugador;
        public Transform puntoLevantarse;

        [Header("Personajes")]
        public ProfesoraMayra mayra;
        public Transform gonza;
        public Transform[] gonzaRutaEntrada;
        [Tooltip("Recorrido de Gonza desde tu banco hasta la gaveta (el último punto es frente a la gaveta).")]
        public Transform[] gonzaRutaGaveta;
        [Tooltip("Los dos últimos puntos son: adentro, frente a la puerta, y afuera en el pasillo.")]
        public Transform[] gonzaRutaSalida;
        public Transform preceptor;
        public Transform preceptorPuntoFinal;

        [Header("Objetos")]
        public Gaveta gaveta;
        public PuertaAula puerta;
        public LlaveEscondida llave;
        public ZonaTrigger zonaFinal;
        public AudioSource audio2D;
        [Tooltip("\"Hmm?\" de hombre (Gonza).")]
        public AudioClip vozHombre;
        [Tooltip("Voz de Laura, la preceptora del final.")]
        public AudioClip vozMujer;

        [Header("Ritmo")]
        public float velocidadGonza = 1.6f;
        public float esperaInsistencia = 7f;

        // Pasos del panel de objetivos
        const int ObjEntregar = 0, ObjLevantarse = 1, ObjLlave = 2, ObjGaveta = 3, ObjFlash = 4, ObjSalir = 5, ObjFin = 6;
        static readonly string[] Objetivos =
        {
            "Entregale el celular a Gonza (E)",
            "Levantate sin que Mayra te escuche",
            "Encontrá la llave abajo del banco",
            "Abrí la gaveta y recuperá tu celular",
            "Encandilá a Mayra con el flash (F)",
            "Salí del aula y doblá a la derecha",
        };

        bool siguiendoConMirada;
        Transform objetivoMirada;
        bool escapando, terminado;
        Coroutine rutinaIntro;
        bool introEnCurso;
        BancoRevisable bancoLlave;

        HUD Hud => HUD.I;

        void Awake() => Instancia = this;

        void Voz(Transform quien)
        {
            if (vozHombre != null && quien != null)
                AudioSource.PlayClipAtPoint(vozHombre, quien.position + Vector3.up * 2.6f, 0.9f);
        }

        // =============================================================================== Inicio

        void Start()
        {
            Hud.FundidoInstantaneo(1f);
            Hud.MostrarHUDJuego(false);
            Hud.DefinirObjetivos(Objetivos);
            Hud.MostrarObjetivos(true);
            Hud.Objetivo("");
            var inv = Inventario.Instancia;
            inv.Agregar(Inventario.Nokia);      // el Nokia de tapita que Gonza no conoce
            inv.Agregar(Inventario.CelularPro); // el celular que te van a pedir

            jugador.Sentar(asientoJugador.position, asientoJugador.eulerAngles.y, puntoLevantarse.position);
            jugador.BloquearMovimiento = true;
            jugador.PuedeLevantarse = false;
            jugador.AlLevantarse += () => { if (!escapando) Hud.AvanzarObjetivo(ObjLlave); };
            interaccion.enabled = false;

            mayra.EmpezarACorregir();
            mayra.Desactivar();

            gaveta.AbrirTapaInstantaneo();
            if (gaveta.candado != null) gaveta.candado.SetActive(false);
            if (gaveta.celularAdentro != null) gaveta.celularAdentro.SetActive(false);
            gaveta.AlAbrir += AlAbrirGaveta;

            gonza.gameObject.SetActive(true);
            gonza.position = gonzaRutaEntrada[0].position;
            preceptor.gameObject.SetActive(false);

            puerta.AlAbrir += () =>
            {
                if (!escapando) return;
                Hud.AvanzarObjetivo(ObjSalir);
                Hud.Objetivo("Al salir, el pasillo está a la derecha.");
            };
            zonaFinal.AlEntrarJugador += () => StartCoroutine(Final());
            flash.AlDisparar += AlUsarFlash;

            ElegirBancoDeLaLlave();

            introEnCurso = true;
            rutinaIntro = StartCoroutine(Intro());
        }

        void ElegirBancoDeLaLlave()
        {
            var bancos = FindObjectsByType<BancoRevisable>(FindObjectsSortMode.None).Where(b => !b.esBancoDelJugador).ToArray();
            if (bancos.Length == 0 || llave == null) { Debug.LogError("[PoliRooms] No hay bancos o falta la llave."); return; }
            bancoLlave = bancos[Random.Range(0, bancos.Length)];
            bancoLlave.AlEncontrarLlave += _ =>
            {
                Hud.AvanzarObjetivo(ObjGaveta);
                Hud.Objetivo("La gaveta está colgada al lado del pizarrón. Abrir el candado hace ruido.");
            };

            // Gonza la tiene en la mano hasta que la esconde.
            llave.transform.localScale = Vector3.one * 2f;
            var anim = gonza.GetComponentInChildren<Animator>();
            Transform mano = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            llave.Sostener(mano != null ? mano : gonza);
        }

        void Update()
        {
            // Si ya moriste una vez en este nivel, podés saltear la intro.
            if (introEnCurso && GestorJuego.MurioEnEsteNivel && !GestorJuego.EstaPausado
                && ControlesJuego.Saltear.WasPressedThisFrame())
                SaltearIntro();

            // Truco de prueba: Ctrl + M saca / vuelve a poner a Mayra.
            if (!GestorJuego.EstaPausado && !terminado && ControlesJuego.TrucoOcultarMayra.WasPressedThisFrame())
            {
                bool activa = !mayra.gameObject.activeSelf;
                mayra.gameObject.SetActive(activa);
                Hud.Aviso(activa ? "Mayra volvió (Ctrl + M)" : "Mayra desapareció (Ctrl + M para que vuelva)", 2.5f);
            }
        }

        // =============================================================================== Intro

        IEnumerator Intro()
        {
            var inv = Inventario.Instancia;
            yield return null;
            if (GestorJuego.MurioEnEsteNivel) Hud.Aviso("Espacio — Saltear la intro", 999f);
            Hud.Fundir(0f, 2f);
            yield return new WaitForSeconds(1.5f);

            // --- Gonza entra ---
            Hud.Subtitulo("Mayra", "Bueno, guarden todo. Empieza la prueba.", 3f);
            yield return new WaitForSeconds(3f);

            jugador.BloquearMirada = true;
            puerta.Abrir(true);
            StartCoroutine(SeguirConMirada(gonza));
            yield return new WaitForSeconds(0.6f);
            yield return MovimientoSimple.Recorrer(gonza, gonzaRutaEntrada.Skip(1).Select(t => t.position).ToArray(), velocidadGonza);
            puerta.Cerrar();
            yield return MovimientoSimple.GirarHacia(gonza, jugador.transform.position, 0.4f);

            Voz(gonza);
            Hud.Subtitulo("Gonza", "A ver vos... el celular. Dale, que no tengo todo el día.", 4f);
            yield return new WaitForSeconds(1f);

            // --- Entregar el celular (E) ---
            siguiendoConMirada = false;
            jugador.BloquearMirada = false;
            float espera = 0f;
            int insistencias = 0;
            string[] insistir =
            {
                "No te hagas el distraído. El celular.",
                "¿Me lo das o lo llamo a Marinelli?"
            };
            while (!ControlesJuego.Interactuar.WasPressedThisFrame())
            {
                Hud.Prompt("E — Entregar el celular", true, 0f);
                espera += Time.deltaTime;
                if (espera > esperaInsistencia && insistencias < insistir.Length)
                {
                    Voz(gonza);
                    Hud.Subtitulo("Gonza", insistir[insistencias++], 3.5f);
                    espera = 0f;
                }
                yield return null;
            }
            Hud.Prompt(null, false, 0f);
            inv.Quitar(Inventario.CelularPro);
            Hud.AvanzarObjetivo(ObjLevantarse);
            Hud.Subtitulo("Gonza", "Gracias. A la gaveta, como siempre.", 3f);

            // --- Gonza guarda el celular ---
            jugador.BloquearMirada = true;
            StartCoroutine(SeguirConMirada(gonza));
            yield return MovimientoSimple.Recorrer(gonza, gonzaRutaGaveta.Select(t => t.position).ToArray(), velocidadGonza);
            yield return MovimientoSimple.GirarHacia(gonza, gaveta.transform.position, 0.4f);
            yield return new WaitForSeconds(0.6f);
            gaveta.GuardarCelularYCerrar();
            yield return new WaitForSeconds(1f);

            // --- Gonza esconde la llave abajo de un banco (se ve dónde) ---
            Hud.Subtitulo("Gonza", "Y la llavecita... la dejo acá abajo, que no me la olvido otra vez.", 4f);
            if (bancoLlave != null)
            {
                Vector3 junto = PuntoJunto(bancoLlave);
                yield return MovimientoSimple.Recorrer(gonza, Camino(gonza.position, junto), velocidadGonza);
                yield return MovimientoSimple.GirarHacia(gonza, bancoLlave.Limites().center, 0.4f);
                yield return new WaitForSeconds(0.4f);
                yield return llave.DejarEn(bancoLlave, 0.8f);
                Hud.Aviso("Gonza dejó la llave abajo de ese banco.", 3f);
                yield return new WaitForSeconds(1.2f);
            }

            // --- Gonza se va ---
            siguiendoConMirada = false;
            jugador.BloquearMirada = false;
            Vector3 frentePuerta = gonzaRutaSalida[gonzaRutaSalida.Length - 2].position;
            yield return MovimientoSimple.Recorrer(gonza, Camino(gonza.position, frentePuerta), velocidadGonza);
            puerta.Abrir(true);
            yield return MovimientoSimple.Recorrer(gonza, new[] { gonzaRutaSalida[gonzaRutaSalida.Length - 1].position }, velocidadGonza);
            puerta.Cerrar();
            gonza.gameObject.SetActive(false);

            Hud.Subtitulo(null, "<i>(Ahora solo tengo que llegar a ese banco sin que Mayra me escuche...)</i>", 4.5f);
            yield return new WaitForSeconds(1.5f);

            EmpezarSigilo();
        }

        /// <summary>Corta la intro y deja todo como queda al final de ella.</summary>
        void SaltearIntro()
        {
            if (!introEnCurso) return;
            if (rutinaIntro != null) StopCoroutine(rutinaIntro);
            siguiendoConMirada = false;

            Inventario.Instancia.Quitar(Inventario.CelularPro);
            if (!gaveta.Cerrada) gaveta.GuardarCelularYCerrar();
            if (puerta.Abierta) puerta.Cerrar();
            if (!llave.Colocada && bancoLlave != null) llave.UbicarEn(bancoLlave);
            gonza.gameObject.SetActive(false);

            Hud.Prompt(null, false, 0f);
            Hud.Subtitulo(null, "", 0.01f);
            Hud.FundidoInstantaneo(0f);
            Hud.AvanzarObjetivo(ObjLevantarse);
            jugador.BloquearMirada = false;
            EmpezarSigilo();
        }

        void EmpezarSigilo()
        {
            introEnCurso = false;
            Hud.Aviso("", 0.01f);

            mayra.EmpezarACorregir();
            Hud.MostrarHUDJuego(true);
            interaccion.enabled = true;
            jugador.BloquearMovimiento = false;
            jugador.BloquearMirada = false;
            jugador.PuedeLevantarse = true;
            Hud.Objetivo("Agachate (C) para moverte en silencio. Si Mayra te busca, volvé a tu banco y apretá E para sentarte.");
            Hud.Aviso("Movete (WASD) para levantarte del banco", 4f);
        }

        /// <summary>Camino por el NavMesh (para que Gonza no atraviese bancos).</summary>
        static Vector3[] Camino(Vector3 desde, Vector3 hasta)
        {
            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(desde, out var a, 2f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(hasta, out var b, 2f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
                && path.corners.Length > 1)
                return path.corners.Skip(1).ToArray();
            return new[] { hasta };
        }

        static Vector3 PuntoJunto(BancoRevisable banco)
        {
            Vector3 c = banco.Limites().center;
            return NavMesh.SamplePosition(c, out var hit, 3f, NavMesh.AllAreas) ? hit.position : c;
        }

        // =============================================================================== Sentarse en tu banco

        public bool MayraTeBusca
        {
            get
            {
                var e = mayra.Estado;
                return e == EstadoMayra.Sospecha || e == EstadoMayra.Buscando
                    || e == EstadoMayra.Persiguiendo || e == EstadoMayra.Patrullando;
            }
        }

        /// <summary>Una vez que sacaste el celular, Mayra cuida la puerta: sentarse ya no sirve.</summary>
        public bool YaNoSirveSentarse => escapando && !terminado;

        public bool PuedeSentarse => !introEnCurso && !escapando && !terminado && !jugador.Sentado && !GestorJuego.EstaTerminado;

        /// <summary>Te sentás en tu banco; si Mayra te estaba buscando, vuelve a corregir.</summary>
        public void SentarseEnBanco()
        {
            if (!PuedeSentarse) return;
            bool teBuscaba = MayraTeBusca;
            jugador.Sentar(asientoJugador.position, asientoJugador.eulerAngles.y, puntoLevantarse.position);
            if (jugador.fuentePasos != null) jugador.fuentePasos.PlayOneShot(AudioProcedural.Silla, 0.4f);
            mayra.JugadorVolvioASuBanco();
            Hud.Aviso(teBuscaba ? "Te hacés el que estudia..." : "Volviste a tu banco. Movete para levantarte.", 2.5f);
        }

        // =============================================================================== Escape

        void AlAbrirGaveta()
        {
            escapando = true;
            mayra.IrACuidarPuerta();
            Hud.AvanzarObjetivo(ObjFlash);
            Hud.Subtitulo(null, "<i>(¡Mi celular! Con este flash la dejo ciega.)</i>", 3f);
            StartCoroutine(GritoMayra());
            Hud.Objetivo("Mayra cuida la puerta. Apuntale y apretá F: el flash del celular la deja ciega unos segundos.");
        }

        IEnumerator GritoMayra()
        {
            yield return new WaitForSeconds(2.5f);
            if (!terminado) Hud.Subtitulo("Mayra", "¿Qué hacés con eso? ¡De acá no salís!", 3f);
        }

        void AlUsarFlash(FlashCelular.Perfil perfil, bool acerto)
        {
            if (escapando && acerto && perfil == flash.pro)
            {
                Hud.AvanzarObjetivo(ObjSalir);
                Hud.Objetivo("¡Ahora! Abrí la puerta (mantené E) y salí al pasillo, a la derecha.");
            }
        }

        IEnumerator SeguirConMirada(Transform t)
        {
            siguiendoConMirada = true;
            objetivoMirada = t;
            while (siguiendoConMirada && objetivoMirada == t && t.gameObject.activeInHierarchy)
            {
                jugador.GirarMiradaHacia(t.position + Vector3.up * 1.5f, Time.deltaTime * 4f);
                yield return null;
            }
        }

        IEnumerator Final()
        {
            if (terminado) yield break;
            terminado = true;

            mayra.Desactivar();
            jugador.BloquearMovimiento = true;
            jugador.BloquearMirada = true;
            interaccion.enabled = false;
            Hud.Prompt(null, false, 0f);
            Hud.MostrarHUDJuego(false);
            Hud.AvanzarObjetivo(ObjFin);
            Hud.Objetivo("");
            Hud.MostrarObjetivos(false);

            preceptor.gameObject.SetActive(true);
            StartCoroutine(SeguirConMirada(preceptor));
            yield return MovimientoSimple.Recorrer(preceptor, new[] { preceptorPuntoFinal.position }, 3f);
            yield return MovimientoSimple.GirarHacia(preceptor, jugador.transform.position, 0.2f);

            if (vozMujer != null) AudioSource.PlayClipAtPoint(vozMujer, preceptor.position + Vector3.up * 2.5f, 0.9f);
            Hud.Subtitulo("Laura", "¿Qué hacés afuera del aula, pibe?", 2.5f);
            yield return new WaitForSeconds(2.3f);

            // Piña
            if (audio2D != null) audio2D.PlayOneShot(AudioProcedural.Golpe);
            Hud.Destello(new Color(1f, 0.85f, 0.8f), 1f, 0.25f);
            siguiendoConMirada = false;
            StartCoroutine(jugador.Sacudon(0.35f, 10f));
            yield return new WaitForSeconds(0.15f);
            Hud.FundidoInstantaneo(1f);

            if (Cordura.Instancia != null) Cordura.Instancia.GuardarParaSiguienteNivel();
            if (GestorJuego.Instancia != null) GestorJuego.Instancia.MarcarTerminado();

            yield return new WaitForSeconds(1.5f);
            Hud.MensajeCentral("NIVEL 2", "LABORATORIO");
            yield return new WaitForSeconds(3.5f);
            Hud.MensajeCentral("Fin de la demo", "Nivel 1 completo.  Presioná R para jugar de nuevo.");

            while (!ControlesJuego.Reiniciar.WasPressedThisFrame()) yield return null;
            Cordura.ValorAlEntrarNivel = 100f;
            GestorJuego.MurioEnEsteNivel = false;
            GestorJuego.Instancia.ReiniciarNivel();
        }
    }
}
