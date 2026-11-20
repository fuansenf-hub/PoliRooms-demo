using System.Collections;
using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// Controlador en primera persona: mirar, caminar, correr (con estamina), agacharse y
    /// estar sentado en el pupitre. Balanceo de cabeza y pasos. Le informa al SistemaRuido
    /// cuánto ruido hace.
    /// Los valores están en la escala del aula (pecera.blend está a 1.6x la escala real).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ControladorJugador : MonoBehaviour
    {
        [Header("Velocidades")]
        public float velocidadCaminar = 5.1f;
        public float velocidadCorrer = 8.6f;
        public float velocidadAgachado = 2.4f;

        [Header("Estamina")]
        public float estaminaMaxima = 4f;
        public float recuperacionEstamina = 0.8f;

        [Header("Alturas")]
        public float alturaNormal = 2.88f;
        public float alturaAgachado = 1.6f;
        public float camaraNormal = 2.56f;
        public float camaraAgachado = 1.2f;
        public float camaraSentado = 1.9f;

        [Header("Mirar")]
        public float sensibilidadMouse = 0.08f;
        public float sensibilidadStick = 140f;
        public Transform camara;

        [Header("Balanceo y pasos")]
        public float zancadaCaminar = 1.7f;
        public float zancadaCorrer = 2.4f;
        public float zancadaAgachado = 1.1f;
        public float balanceoCaminar = 0.06f;
        public float balanceoCorrer = 0.11f;
        public float balanceoAgachado = 0.03f;
        public AudioSource fuentePasos;
        [Tooltip("Clip real de pasos en loop (si falta, se usan pasos generados).")]
        public AudioClip clipPasosLoop;

        public float gravedad = -24f;

        [HideInInspector] public bool BloquearMovimiento;
        [HideInInspector] public bool BloquearMirada;
        [HideInInspector] public bool PuedeLevantarse = true;

        public bool Sentado { get; private set; }
        public bool Agachado { get; private set; }
        public bool Corriendo { get; private set; }
        public bool EnMovimiento { get; private set; }
        public float Estamina01 => estaminaMaxima > 0f ? estamina / estaminaMaxima : 1f;
        public Vector3 PosicionCamara => camara.position;

        public event System.Action AlLevantarse;

        CharacterController cc;
        float yaw, pitch, velY, estamina;
        bool exhausto;
        Vector3 puntoLevantarse;
        float alturaCamaraBase, distanciaPaso, balanceo;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (camara == null) camara = GetComponentInChildren<Camera>().transform;
            yaw = transform.eulerAngles.y;
            estamina = estaminaMaxima;
            alturaCamaraBase = camara.localPosition.y;
            cam = camara.GetComponent<Camera>();
            if (cam != null) fovBase = cam.fieldOfView;
            ControlesJuego.Asegurar();
        }

        [Header("Zoom (Z + rueda del mouse)")]
        [Tooltip("Zoom con el que arranca al apretar Z (1 = 100%).")]
        public float zoomInicial = 1.5f;
        public float zoomMinimo = 1.1f;
        public float zoomMaximo = 2f;
        public float pasoRueda = 0.1f;
        Camera cam;
        float fovBase = 70f, zoomElegido = -1f, zoomActual = 1f;

        /// <summary>Zoom actual (1 = sin zoom). La sensibilidad del mouse baja con el zoom.</summary>
        public float ZoomActual => zoomActual;

        void ActualizarZoom(float dt)
        {
            if (cam == null) return;
            if (zoomElegido < 0f) zoomElegido = zoomInicial;
            bool manteniendo = !BloquearMirada && ControlesJuego.Zoom.IsPressed();
            if (manteniendo)
            {
                float rueda = ControlesJuego.RuedaZoom.ReadValue<float>();
                if (Mathf.Abs(rueda) > 0.01f)
                    zoomElegido = Mathf.Clamp(zoomElegido + Mathf.Sign(rueda) * pasoRueda, zoomMinimo, zoomMaximo);
            }
            if (HUD.I != null) HUD.I.IndicadorZoom(manteniendo ? zoomElegido : 0f);
            float objetivo = manteniendo ? zoomElegido : 1f;
            zoomActual = Mathf.Lerp(zoomActual, objetivo, dt * 12f);
            // Zoom real: se achica el ángulo de visión de forma que la imagen se agrande 'zoomActual' veces.
            float mitad = Mathf.Atan(Mathf.Tan(fovBase * 0.5f * Mathf.Deg2Rad) / zoomActual) * Mathf.Rad2Deg;
            cam.fieldOfView = mitad * 2f;
        }

        void Update()
        {
            if (GestorJuego.EstaPausado) return;
            float dt = Time.deltaTime;
            ActualizarZoom(dt);

            // --- Mirar ---
            if (!BloquearMirada)
            {
                Vector2 d = (ControlesJuego.Mirar.ReadValue<Vector2>() * sensibilidadMouse
                          + ControlesJuego.MirarStick.ReadValue<Vector2>() * sensibilidadStick * dt) / zoomActual;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            }
            AplicarRotacion();

            Vector2 entrada = BloquearMovimiento ? Vector2.zero : ControlesJuego.Mover.ReadValue<Vector2>();

            // --- Sentado en el pupitre ---
            if (Sentado)
            {
                Agachado = Corriendo = EnMovimiento = false;
                ActualizarPasosLoop(false);
                MoverCamaraA(camaraSentado, 0f, dt);
                if (SistemaRuido.Instancia != null) SistemaRuido.Instancia.ReportarMovimiento(0f);
                if (!BloquearMovimiento && PuedeLevantarse && entrada.sqrMagnitude > 0.25f) Levantarse();
                return;
            }

            // --- Agacharse ---
            Agachado = !BloquearMovimiento && ControlesJuego.Agacharse.IsPressed();
            float alturaObj = Agachado ? alturaAgachado : alturaNormal;
            cc.height = Mathf.Lerp(cc.height, alturaObj, dt * 10f);
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

            // --- Estamina y sprint ---
            if (estamina <= 0f) exhausto = true;
            else if (estamina >= estaminaMaxima * 0.5f) exhausto = false;

            EnMovimiento = entrada.sqrMagnitude > 0.01f;
            Corriendo = EnMovimiento && !Agachado && !exhausto && ControlesJuego.Correr.IsPressed();
            if (Corriendo) estamina -= dt;
            else estamina += dt * recuperacionEstamina;
            estamina = Mathf.Clamp(estamina, 0f, estaminaMaxima);

            // --- Movimiento ---
            float vel = Agachado ? velocidadAgachado : (Corriendo ? velocidadCorrer : velocidadCaminar);
            Vector3 dir = transform.right * entrada.x + transform.forward * entrada.y;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            if (cc.isGrounded && velY < 0f) velY = -2f;
            velY += gravedad * dt;
            Vector3 antes = transform.position;
            cc.Move((dir * vel + Vector3.up * velY) * dt);
            Vector3 movido = transform.position - antes; movido.y = 0f;

            // --- Balanceo de cabeza y pasos ---
            float zancada = Agachado ? zancadaAgachado : Corriendo ? zancadaCorrer : zancadaCaminar;
            float amplitud = Agachado ? balanceoAgachado : Corriendo ? balanceoCorrer : balanceoCaminar;
            bool caminando = EnMovimiento && cc.isGrounded && movido.sqrMagnitude > 0.000001f;
            if (caminando)
            {
                distanciaPaso += movido.magnitude;
                if (distanciaPaso >= zancada)
                {
                    distanciaPaso -= zancada;
                    if (clipPasosLoop == null) ReproducirPaso();
                }
                balanceo = -Mathf.Abs(Mathf.Sin(distanciaPaso / zancada * Mathf.PI)) * amplitud;
            }
            else balanceo = Mathf.Lerp(balanceo, 0f, dt * 8f);
            ActualizarPasosLoop(caminando);
            MoverCamaraA(Agachado ? camaraAgachado : camaraNormal, balanceo, dt);

            // --- Ruido ---
            if (SistemaRuido.Instancia != null)
            {
                var r = SistemaRuido.Instancia;
                float radio = !EnMovimiento ? 0f : Corriendo ? r.radioCorriendo : Agachado ? r.radioAgachado : r.radioCaminando;
                r.ReportarMovimiento(radio);
            }
        }

        AudioSource fuenteLoop;

        /// <summary>Pasos con un clip real en loop: se acelera al correr y baja al agacharse.</summary>
        void ActualizarPasosLoop(bool caminando)
        {
            if (clipPasosLoop == null) return;
            if (fuenteLoop == null)
            {
                fuenteLoop = gameObject.AddComponent<AudioSource>();
                fuenteLoop.clip = clipPasosLoop;
                fuenteLoop.loop = true;
                fuenteLoop.playOnAwake = false;
                fuenteLoop.spatialBlend = 0f;
            }
            if (caminando)
            {
                fuenteLoop.pitch = Agachado ? 0.8f : Corriendo ? 1.45f : 1.05f;
                fuenteLoop.volume = Agachado ? 0.15f : Corriendo ? 0.7f : 0.4f;
                if (!fuenteLoop.isPlaying) fuenteLoop.Play();
            }
            else if (fuenteLoop.isPlaying) fuenteLoop.Pause();
        }

        void ReproducirPaso()
        {
            if (fuentePasos == null) return;
            fuentePasos.pitch = Random.Range(0.9f, 1.1f) * (Corriendo ? 1.1f : 1f);
            float volumen = Agachado ? 0.12f : Corriendo ? 0.55f : 0.3f;
            fuentePasos.PlayOneShot(AudioProcedural.Paso, volumen);
        }

        void MoverCamaraA(float altura, float extra, float dt)
        {
            alturaCamaraBase = Mathf.Lerp(alturaCamaraBase, altura, dt * 10f);
            Vector3 p = camara.localPosition;
            p.y = alturaCamaraBase + extra;
            camara.localPosition = p;
        }

        void AplicarRotacion()
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            camara.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>Sienta al jugador en el pupitre. Al moverse se levanta hacia 'levantarse'.</summary>
        public void Sentar(Vector3 posicion, float yawMirando, Vector3 levantarse)
        {
            Sentado = true;
            cc.enabled = false;
            transform.position = posicion;
            yaw = yawMirando;
            pitch = 8f;
            puntoLevantarse = levantarse;
            alturaCamaraBase = camaraSentado;
            Vector3 p = camara.localPosition; p.y = camaraSentado; camara.localPosition = p;
            AplicarRotacion();
        }

        public void Levantarse()
        {
            if (!Sentado) return;
            Sentado = false;
            transform.position = puntoLevantarse;
            cc.enabled = true;
            if (fuentePasos != null) fuentePasos.PlayOneShot(AudioProcedural.Silla, 0.5f);
            if (SistemaRuido.Instancia != null) SistemaRuido.Instancia.Pulso(4.5f, 0.6f); // la silla raspa el piso
            AlLevantarse?.Invoke();
        }

        /// <summary>Gira la cámara suavemente hacia un punto (para cinemáticas).</summary>
        public IEnumerator MirarHacia(Vector3 punto, float duracion)
        {
            Vector3 d = punto - camara.position;
            float yawObj = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitchObj = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            float yaw0 = yaw, pitch0 = pitch, t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, duracion)));
                yaw = Mathf.LerpAngle(yaw0, yawObj, k);
                pitch = Mathf.Lerp(pitch0, Mathf.Clamp(pitchObj, -85f, 85f), k);
                AplicarRotacion();
                yield return null;
            }
        }

        /// <summary>Acerca la mirada hacia un punto (llamar cada frame para seguir algo).</summary>
        public void GirarMiradaHacia(Vector3 punto, float factor)
        {
            Vector3 d = punto - camara.position;
            float yawObj = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitchObj = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            yaw = Mathf.LerpAngle(yaw, yawObj, factor);
            pitch = Mathf.Lerp(pitch, Mathf.Clamp(pitchObj, -85f, 85f), factor);
            AplicarRotacion();
        }

        /// <summary>Sacudón de cámara (piña, susto).</summary>
        public IEnumerator Sacudon(float duracion, float fuerza)
        {
            float t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                float f = fuerza * (1f - t / duracion);
                camara.localRotation = Quaternion.Euler(pitch + Random.Range(-f, f), Random.Range(-f, f), Random.Range(-f, f) * 2f);
                yield return null;
            }
            AplicarRotacion();
        }
    }
}
