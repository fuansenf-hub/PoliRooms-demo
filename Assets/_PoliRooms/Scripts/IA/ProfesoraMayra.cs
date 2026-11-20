using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace PoliRooms
{
    public enum EstadoMayra
    {
        Inactiva,       // cinemáticas
        Corrigiendo,    // sentada en el escritorio corrigiendo pruebas (fase de sigilo)
        Sospecha,       // escuchó algo y mira hacia tu banco
        Buscando,       // va hacia donde escuchó/vio algo
        Persiguiendo,   // te vio: te persigue
        Patrullando,    // ya está alerta, recorre el aula
        Aturdida,       // encandilada por el flash
        CuidandoPuerta, // se planta en la puerta para que no salgas
        Atrapando,      // te agarró
        VolviendoAlEscritorio // volviste a tu banco: se le pasa y vuelve a corregir
    }

    /// <summary>
    /// IA de Mayra. Empieza corrigiendo pruebas; si escucha ruido y ve que no estás en tu
    /// banco, te empieza a buscar. Ve con un cono de visión (bloqueado por paredes y muebles)
    /// y escucha usando el SistemaRuido. El flash la aturde.
    /// El modelo es una "estatua" sin animaciones: se desliza, y se inclina para corregir.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ProfesoraMayra : MonoBehaviour, IAturdible
    {
        [Header("Referencias")]
        public Transform visual;
        public Transform puntoEscritorio;
        public Transform puntoPuerta;
        public Transform[] puntosPatrulla;
        public Transform asientoJugador;
        public float radioAsiento = 1.1f;

        [Header("Sentidos")]
        public float distanciaVision = 22f;
        public float anguloVision = 70f;
        public float alturaOjos = 2.5f;

        [Header("Movimiento")]
        public float velocidadPatrulla = 2.6f;
        public float velocidadBusqueda = 3.7f;
        public float velocidadPersecucion = 5.6f;
        public float distanciaAtrapar = 1.6f;

        [Header("Tiempos")]
        public float tiempoOlvido = 4f;
        public float duracionBusqueda = 10f;
        public float duracionSospecha = 1.6f;
        public Vector2 intervaloLevantarVista = new Vector2(9f, 16f);
        public float duracionLevantarVista = 2.5f;

        [Header("Cordura que drena por segundo")]
        public float drenajePersecucion = 4f;
        public float drenajeBusqueda = 1.2f;
        public float drenajePatrulla = 0.4f;
        public float drenajeVigilando = 2f;

        [Header("Audio")]
        public AudioSource audioPasos;
        public AudioSource audioSusto;
        public AudioClip clipSusto;
        public AudioSource audioDeteccion; // jadeo del jugador al ser visto
        public AudioClip clipDeteccion;
        [Tooltip("Fuente 3D para la lapicera mientras corrige.")]
        public AudioSource audioLapicera;
        [Tooltip("Fuente 3D para la voz de Mayra.")]
        public AudioSource audioVoz;
        public AudioClip clipSospecha;

        [Header("Animación (modelo humanoide)")]
        public AnimadorPersonaje animador;
        public float duracionLevantarse = 2.2f;

        /// <summary>Está sentada en su silla (corrigiendo o mirando).</summary>
        public bool Sentada { get; private set; }
        float levantandose;

        public EstadoMayra Estado { get; private set; } = EstadoMayra.Inactiva;
        public bool VeAlJugador { get; private set; }
        public Vector3 PuntoObjetivo => transform.position + Vector3.up * 1.9f;
        public Vector3 PosicionCabeza => transform.position + Vector3.up * alturaOjos;

        public event System.Action<EstadoMayra> AlCambiarEstado;

        NavMeshAgent agente;
        Transform jugador;
        ControladorJugador ctrlJugador;
        float timer, timerVista, timerAturdida;
        bool mirandoArriba;
        Vector3 ultimaPosicion;
        EstadoMayra estadoAntesDeAturdir;
        int indicePatrulla;
        float inclinacion, timerLapicera;

        void Awake()
        {
            agente = GetComponent<NavMeshAgent>();
            var obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null)
            {
                jugador = obj.transform;
                ctrlJugador = obj.GetComponent<ControladorJugador>();
            }
            timerVista = Random.Range(intervaloLevantarVista.x, intervaloLevantarVista.y);
        }

        // ------------------------------------------------------------------ API

        public void CambiarEstado(EstadoMayra nuevo)
        {
            if (Estado == nuevo) return;
            var anterior = Estado;
            Estado = nuevo;
            timer = 0f;

            // Si estaba sentada y ahora tiene que moverse, primero se para de la silla.
            bool seMueve = nuevo == EstadoMayra.Buscando || nuevo == EstadoMayra.Persiguiendo
                        || nuevo == EstadoMayra.Patrullando || nuevo == EstadoMayra.CuidandoPuerta
                        || nuevo == EstadoMayra.VolviendoAlEscritorio;
            if (Sentada && seMueve)
            {
                Sentada = false;
                levantandose = duracionLevantarse;
            }

            if (agente.isOnNavMesh)
            {
                agente.isStopped = !seMueve || levantandose > 0f;
                agente.speed = nuevo == EstadoMayra.Persiguiendo ? velocidadPersecucion
                             : nuevo == EstadoMayra.Patrullando ? velocidadPatrulla
                             : velocidadBusqueda;
            }

            if (nuevo == EstadoMayra.Sospecha && audioVoz != null && clipSospecha != null)
                audioVoz.PlayOneShot(clipSospecha);

            if (nuevo == EstadoMayra.Persiguiendo && anterior != EstadoMayra.Aturdida)
            {
                if (audioDeteccion != null)
                {
                    if (clipDeteccion != null) audioDeteccion.PlayOneShot(clipDeteccion);
                    audioDeteccion.PlayOneShot(AudioProcedural.Susto, 0.8f);
                }
                if (HUD.I != null) HUD.I.Aviso("¡Mayra te vio!", 1.5f);
            }
            AlCambiarEstado?.Invoke(nuevo);
        }

        public void EmpezarACorregir()
        {
            if (puntoEscritorio != null) Teletransportar(puntoEscritorio.position, puntoEscritorio.rotation);
            Sentada = true;
            levantandose = 0f;
            CambiarEstado(EstadoMayra.Corrigiendo);
        }

        public void IrACuidarPuerta()
        {
            CambiarEstado(EstadoMayra.CuidandoPuerta);
            if (puntoPuerta != null && agente.isOnNavMesh)
            {
                agente.speed = velocidadPersecucion;
                agente.SetDestination(puntoPuerta.position);
            }
        }

        public void Desactivar()
        {
            CambiarEstado(EstadoMayra.Inactiva);
            if (audioPasos != null) audioPasos.Stop();
        }

        public void Teletransportar(Vector3 pos, Quaternion rot)
        {
            if (agente.isOnNavMesh) agente.Warp(pos); else transform.position = pos;
            transform.rotation = rot;
        }

        public void Aturdir(float segundos)
        {
            if (Estado == EstadoMayra.Inactiva || Estado == EstadoMayra.Atrapando) return;
            if (Estado != EstadoMayra.Aturdida) estadoAntesDeAturdir = Estado;
            timerAturdida = Mathf.Max(timerAturdida, segundos);
            CambiarEstado(EstadoMayra.Aturdida);
            if (HUD.I != null) HUD.I.Aviso(segundos >= 3f ? "¡Mayra quedó encandilada!" : "Mayra se tapa los ojos...", 1.5f);
        }

        // ------------------------------------------------------------------ Bucle

        void Update()
        {
            if (jugador == null || GestorJuego.EstaPausado) return;
            float dt = Time.deltaTime;

            VeAlJugador = Estado != EstadoMayra.Inactiva && Estado != EstadoMayra.Aturdida
                       && Estado != EstadoMayra.Atrapando && PuedeVerJugador();
            bool oye = Estado != EstadoMayra.Inactiva && Estado != EstadoMayra.Aturdida && PuedeOirJugador();

            // Parándose de la silla: no se mueve hasta terminar la animación.
            if (levantandose > 0f)
            {
                levantandose -= dt;
                if (agente.isOnNavMesh) agente.isStopped = levantandose > 0f;
                if (levantandose <= 0f && agente.isOnNavMesh && Estado == EstadoMayra.CuidandoPuerta && puntoPuerta != null)
                    agente.SetDestination(puntoPuerta.position);
                if (levantandose > 0f)
                {
                    DrenarCordura();
                    ActualizarVisual(dt);
                    ActualizarAnimacion();
                    ActualizarPasos();
                    return;
                }
            }

            // ¿Te agarró?
            if (Estado != EstadoMayra.Inactiva && Estado != EstadoMayra.Aturdida && Estado != EstadoMayra.Atrapando
                && Estado != EstadoMayra.Corrigiendo && Estado != EstadoMayra.Sospecha
                && Estado != EstadoMayra.VolviendoAlEscritorio && !(ctrlJugador != null && ctrlJugador.Sentado)
                && !GestorJuego.EstaTerminado && DistanciaHorizontal() <= distanciaAtrapar)
            {
                StartCoroutine(Atrapar());
                return;
            }

            switch (Estado)
            {
                case EstadoMayra.Corrigiendo: ActualizarCorrigiendo(dt, oye); break;
                case EstadoMayra.Sospecha: ActualizarSospecha(dt); break;
                case EstadoMayra.Buscando: ActualizarBuscando(dt, oye); break;
                case EstadoMayra.Persiguiendo: ActualizarPersiguiendo(dt, oye); break;
                case EstadoMayra.Patrullando: ActualizarPatrullando(dt, oye); break;
                case EstadoMayra.Aturdida: ActualizarAturdida(dt); break;
                case EstadoMayra.CuidandoPuerta: ActualizarCuidandoPuerta(dt); break;
                case EstadoMayra.VolviendoAlEscritorio: ActualizarVolviendo(oye); break;
            }

            DrenarCordura();
            ActualizarVisual(dt);
            ActualizarAnimacion();
            ActualizarPasos();
        }

        void ActualizarAnimacion()
        {
            if (animador == null) return;
            int a;
            if (levantandose > 0f) a = AnimadorPersonaje.Levantarse;
            else if (Sentada)
                a = Estado == EstadoMayra.Inactiva ? AnimadorPersonaje.Sentado
                  : Estado == EstadoMayra.Corrigiendo && !mirandoArriba ? AnimadorPersonaje.Escribiendo
                  : AnimadorPersonaje.SentadaMirando;
            else if (Estado == EstadoMayra.Buscando) a = AnimadorPersonaje.Sigilo;
            else a = AnimadorPersonaje.Locomocion;
            animador.SetAccion(a);
        }

        void ActualizarCorrigiendo(float dt, bool oye)
        {
            if (puntoEscritorio != null)
                transform.rotation = Quaternion.Slerp(transform.rotation, puntoEscritorio.rotation, dt * 3f);

            // Lapicera rayando las pruebas (también ayuda a ubicarla de oído).
            timerLapicera -= dt;
            if (!mirandoArriba && timerLapicera <= 0f && audioLapicera != null)
            {
                audioLapicera.pitch = Random.Range(0.85f, 1.15f);
                audioLapicera.PlayOneShot(AudioProcedural.Lapicera, 0.6f);
                timerLapicera = Random.Range(0.5f, 1.8f);
            }

            // De vez en cuando levanta la vista de las pruebas.
            timerVista -= dt;
            if (!mirandoArriba && timerVista <= 0f)
            {
                mirandoArriba = true;
                timerVista = duracionLevantarVista;
                if (HUD.I != null) HUD.I.Aviso("Mayra levanta la vista...", duracionLevantarVista);
            }
            else if (mirandoArriba && timerVista <= 0f)
            {
                mirandoArriba = false;
                timerVista = Random.Range(intervaloLevantarVista.x, intervaloLevantarVista.y);
            }

            if (mirandoArriba && VeAlJugador && !JugadorEnSuBanco())
            {
                ultimaPosicion = jugador.position;
                IrA(EstadoMayra.Persiguiendo);
                return;
            }

            if (oye && !JugadorEnSuBanco())
            {
                ultimaPosicion = jugador.position;
                mirandoArriba = true;
                CambiarEstado(EstadoMayra.Sospecha);
                if (HUD.I != null) HUD.I.Aviso("Mayra escuchó algo...", duracionSospecha);
            }
        }

        void ActualizarSospecha(float dt)
        {
            timer += dt;
            GirarHacia(ultimaPosicion, dt);
            if (VeAlJugador) { ultimaPosicion = jugador.position; IrA(EstadoMayra.Persiguiendo); return; }
            if (timer < duracionSospecha) return;

            if (JugadorEnSuBanco())
            {
                // Volviste a tu banco a tiempo: sigue corrigiendo.
                mirandoArriba = false;
                timerVista = Random.Range(intervaloLevantarVista.x, intervaloLevantarVista.y);
                CambiarEstado(EstadoMayra.Corrigiendo);
            }
            else
            {
                if (HUD.I != null) HUD.I.Subtitulo("Mayra", "¿Y vos? ¿Dónde te metiste?", 2.5f);
                IrA(EstadoMayra.Buscando);
            }
        }

        void ActualizarBuscando(float dt, bool oye)
        {
            timer += dt;
            if (VeAlJugador) { IrA(EstadoMayra.Persiguiendo); return; }
            if (oye) { ultimaPosicion = jugador.position; agente.SetDestination(ultimaPosicion); }

            if (!agente.pathPending && agente.remainingDistance <= 0.6f)
            {
                // Llegó: mira alrededor y prueba otro punto cercano.
                transform.Rotate(0f, 90f * dt, 0f);
                if (timer % 3f < dt) agente.SetDestination(PuntoCercano(ultimaPosicion, 5f));
            }
            if (timer >= duracionBusqueda) IrA(EstadoMayra.Patrullando);
        }

        void ActualizarPersiguiendo(float dt, bool oye)
        {
            if (VeAlJugador || oye)
            {
                ultimaPosicion = jugador.position;
                timer = 0f;
            }
            else timer += dt;

            agente.SetDestination(ultimaPosicion);
            if (timer >= tiempoOlvido) IrA(EstadoMayra.Buscando);
        }

        void ActualizarPatrullando(float dt, bool oye)
        {
            if (VeAlJugador) { ultimaPosicion = jugador.position; IrA(EstadoMayra.Persiguiendo); return; }
            if (oye) { ultimaPosicion = jugador.position; IrA(EstadoMayra.Buscando); return; }

            if (!agente.pathPending && agente.remainingDistance <= 0.6f)
            {
                timer += dt;
                if (timer > 1.5f)
                {
                    timer = 0f;
                    agente.SetDestination(SiguientePuntoPatrulla());
                }
            }
        }

        void ActualizarAturdida(float dt)
        {
            timerAturdida -= dt;
            if (timerAturdida > 0f) return;

            ultimaPosicion = jugador.position;
            if (estadoAntesDeAturdir == EstadoMayra.CuidandoPuerta) IrACuidarPuerta();
            else IrA(EstadoMayra.Buscando); // sabe más o menos dónde estabas
        }

        /// <summary>
        /// El jugador volvió a sentarse en su banco: si lo estaba buscando o persiguiendo,
        /// "se le pasa" y vuelve a su escritorio a corregir. (No aplica si cuida la puerta.)
        /// </summary>
        public void JugadorVolvioASuBanco()
        {
            bool alerta = Estado == EstadoMayra.Sospecha || Estado == EstadoMayra.Buscando
                       || Estado == EstadoMayra.Persiguiendo || Estado == EstadoMayra.Patrullando;
            if (!alerta) return;

            if (Estado == EstadoMayra.Sospecha)
            {
                // Seguía sentada: directamente vuelve a corregir.
                mirandoArriba = false;
                timerVista = Random.Range(intervaloLevantarVista.x, intervaloLevantarVista.y);
                CambiarEstado(EstadoMayra.Corrigiendo);
                return;
            }
            CambiarEstado(EstadoMayra.VolviendoAlEscritorio);
            if (agente.isOnNavMesh && puntoEscritorio != null)
            {
                agente.speed = velocidadPatrulla;
                agente.SetDestination(puntoEscritorio.position);
            }
            if (HUD.I != null) HUD.I.Subtitulo("Mayra", "Mmm... me habrá parecido.", 2.5f);
        }

        void ActualizarVolviendo(bool oye)
        {
            // Si te levantás de nuevo y te ve o te escucha, vuelve a la carga.
            if (!JugadorEnSuBanco())
            {
                if (VeAlJugador) { ultimaPosicion = jugador.position; IrA(EstadoMayra.Persiguiendo); return; }
                if (oye) { ultimaPosicion = jugador.position; IrA(EstadoMayra.Buscando); return; }
            }
            if (puntoEscritorio != null && !agente.pathPending && agente.remainingDistance <= 0.6f)
                EmpezarACorregir();
        }

        void ActualizarCuidandoPuerta(float dt)
        {
            if (puntoPuerta == null) return;
            bool llego = !agente.pathPending && agente.remainingDistance <= 0.4f;
            if (llego)
            {
                if (VeAlJugador) GirarHacia(jugador.position, dt);
                else transform.rotation = Quaternion.Slerp(transform.rotation, puntoPuerta.rotation, dt * 4f);
            }
            else agente.SetDestination(puntoPuerta.position);
        }

        // ------------------------------------------------------------------ Helpers

        void IrA(EstadoMayra estado)
        {
            CambiarEstado(estado);
            if (!agente.isOnNavMesh) return;
            if (estado == EstadoMayra.Buscando || estado == EstadoMayra.Persiguiendo)
                agente.SetDestination(ultimaPosicion);
            else if (estado == EstadoMayra.Patrullando)
                agente.SetDestination(SiguientePuntoPatrulla());
        }

        bool PuedeVerJugador()
        {
            Vector3 ojos = PosicionCabeza;
            Vector3 objetivo = ctrlJugador != null ? ctrlJugador.PosicionCamara : jugador.position + Vector3.up * 1.5f;
            Vector3 d = objetivo - ojos;
            if (d.magnitude > distanciaVision) return false;
            Vector3 frente = transform.forward;
            if (Vector3.Angle(frente, d) > anguloVision) return false;
            // Mayra y el jugador están en la capa "Ignore Raycast": solo bloquean paredes y muebles.
            return !Physics.Linecast(ojos, objetivo, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }

        bool PuedeOirJugador()
        {
            var r = SistemaRuido.Instancia;
            if (r == null) return false;
            float radio = r.Radio;
            return radio > 0.01f && Vector3.Distance(transform.position, jugador.position) <= radio;
        }

        bool JugadorEnSuBanco()
        {
            if (ctrlJugador != null && ctrlJugador.Sentado) return true;
            if (asientoJugador == null) return false;
            Vector3 d = jugador.position - asientoJugador.position; d.y = 0f;
            return d.magnitude <= radioAsiento;
        }

        float DistanciaHorizontal()
        {
            Vector3 d = jugador.position - transform.position; d.y = 0f;
            return d.magnitude;
        }

        void GirarHacia(Vector3 punto, float dt)
        {
            Vector3 d = punto - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), dt * 5f);
        }

        Vector3 SiguientePuntoPatrulla()
        {
            if (puntosPatrulla == null || puntosPatrulla.Length == 0) return PuntoCercano(transform.position, 8f);
            indicePatrulla = (indicePatrulla + Random.Range(1, Mathf.Max(2, puntosPatrulla.Length))) % puntosPatrulla.Length;
            return puntosPatrulla[indicePatrulla].position;
        }

        static Vector3 PuntoCercano(Vector3 centro, float radio)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = centro + Random.insideUnitSphere * radio;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return hit.position;
            }
            return centro;
        }

        void DrenarCordura()
        {
            var c = Cordura.Instancia;
            if (c == null) return;
            switch (Estado)
            {
                case EstadoMayra.Persiguiendo: c.Drenar(drenajePersecucion); break;
                case EstadoMayra.Buscando: c.Drenar(drenajeBusqueda); break;
                case EstadoMayra.Patrullando: c.Drenar(drenajePatrulla); break;
                case EstadoMayra.CuidandoPuerta: if (VeAlJugador) c.Drenar(drenajeVigilando); break;
            }
        }

        void ActualizarVisual(float dt)
        {
            if (visual == null) return;
            // Corrigiendo con la cabeza gacha; se endereza cuando levanta la vista.
            // (con modelo animado, la pose la da la animación: solo se inclina hacia atrás si la encandilan)
            float objetivo = animador == null && Estado == EstadoMayra.Corrigiendo && !mirandoArriba ? 14f
                           : Estado == EstadoMayra.Aturdida ? -12f : 0f;
            inclinacion = Mathf.Lerp(inclinacion, objetivo, dt * 4f);
            visual.localRotation = Quaternion.Euler(inclinacion, 0f, 0f);

            // Temblequeo cuando está encandilada o persiguiendo (efecto "maniquí").
            float temblor = Estado == EstadoMayra.Aturdida ? 0.03f : Estado == EstadoMayra.Persiguiendo ? 0.006f : 0f;
            visual.localPosition = temblor > 0f ? Random.insideUnitSphere * temblor : Vector3.zero;
        }

        void ActualizarPasos()
        {
            if (audioPasos == null) return;
            bool moviendo = agente.isOnNavMesh && !agente.isStopped && agente.velocity.sqrMagnitude > 0.05f;
            if (moviendo)
            {
                if (!audioPasos.isPlaying) audioPasos.Play();
                audioPasos.pitch = Estado == EstadoMayra.Persiguiendo ? 1.3f : 1f;
            }
            else if (audioPasos.isPlaying) audioPasos.Pause();
        }

        IEnumerator Atrapar()
        {
            CambiarEstado(EstadoMayra.Atrapando);
            if (audioPasos != null) audioPasos.Stop();
            if (audioSusto != null && clipSusto != null) audioSusto.PlayOneShot(clipSusto);
            if (ctrlJugador != null)
            {
                ctrlJugador.BloquearMovimiento = true;
                ctrlJugador.BloquearMirada = true;
                StartCoroutine(ctrlJugador.MirarHacia(PosicionCabeza, 0.35f));
            }
            GirarHacia(jugador.position, 1f);
            if (HUD.I != null) HUD.I.Destello(new Color(0.6f, 0f, 0f), 0.5f, 0.6f);
            yield return new WaitForSeconds(1.3f);
            if (GestorJuego.Instancia != null) GestorJuego.Instancia.Morir("Mayra te atrapó.");
        }
    }
}
