using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// La llave pegada abajo de un banco. Pistas para encontrarla:
    ///  1) Un tintineo 3D cada pocos segundos (se escucha mejor si hacés silencio).
    ///  2) Cada tintineo que llega a oírse muestra un texto con la dirección y qué tan cerca está.
    ///  3) Un brillito dorado abajo del banco, que se ve de cerca y agachado.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class LlaveEscondida : MonoBehaviour
    {
        public Vector2 intervalo = new Vector2(5f, 8f);
        [Range(0f, 1f)] public float volumen = 0.35f;
        public float alcance = 14f;
        [Tooltip("Sonido real de las llaves (si falta, se usa uno generado).")]
        public AudioClip clip;
        public AudioClip Clip => clip != null ? clip : AudioProcedural.Tintineo;

        [Header("Pistas")]
        public bool mostrarPistaEnPantalla = true;
        public float distanciaCerca = 7f;
        public float distanciaMuyCerca = 3.5f;

        AudioSource fuente;
        Light brillo;
        Transform cam;
        float timer;
        bool recogida;

        void Awake()
        {
            fuente = GetComponent<AudioSource>();
            fuente.playOnAwake = false;
            fuente.spatialBlend = 1f;
            fuente.rolloffMode = AudioRolloffMode.Linear;
            fuente.minDistance = 0.8f;
            fuente.maxDistance = alcance;
            fuente.dopplerLevel = 0f;
            timer = Random.Range(1f, intervalo.y);

            var go = new GameObject("Brillo");
            go.transform.SetParent(transform, false);
            brillo = go.AddComponent<Light>();
            brillo.type = LightType.Point;
            brillo.color = new Color(1f, 0.8f, 0.42f);
            brillo.range = 1.8f;
            brillo.intensity = intensidadLuz;
            brillo.shadows = LightShadows.None;

            // La llave misma brilla un poquito (emisión dorada tenue).
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var m = r.material; // instancia propia
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.3f) * 0.35f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        [Tooltip("Luz que emite la llave: muy tenue.")]
        public float intensidadLuz = 0.45f;

        void Start()
        {
            var j = FindFirstObjectByType<ControladorJugador>();
            if (j != null) cam = j.camara;
        }

        /// <summary>Mueve la llave abajo del banco elegido.</summary>
        public void UbicarEn(BancoRevisable banco)
        {
            Bounds b = banco.Limites();
            // Debajo del tablero: un poco por debajo de la mitad de la altura del pupitre.
            transform.position = new Vector3(b.center.x, b.min.y + b.size.y * 0.5f, b.center.z);
            transform.SetParent(banco.transform, true); // conserva el tamaño en el mundo
            banco.tieneLlave = true;
            banco.llave = this;
            Colocada = true;
            timer = Random.Range(2f, intervalo.y);
        }

        /// <summary>La llave ya está escondida bajo un banco (antes la tiene Gonza en la mano).</summary>
        public bool Colocada { get; private set; }

        /// <summary>Gonza lleva la llave en la mano.</summary>
        public void Sostener(Transform mano)
        {
            Colocada = false;
            transform.SetParent(mano, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            // La mano del modelo puede venir escalada: mantenemos el tamaño real de la llave.
            Vector3 s = mano.lossyScale;
            transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z)) * 2f;
        }

        /// <summary>La desliza desde la mano de Gonza hasta abajo del banco (para que se vea dónde la deja).</summary>
        public System.Collections.IEnumerator DejarEn(BancoRevisable banco, float duracion)
        {
            Vector3 desde = transform.position;
            transform.SetParent(null, true);
            transform.localScale = Vector3.one * 2f;
            Bounds b = banco.Limites();
            Vector3 hasta = new Vector3(b.center.x, b.min.y + b.size.y * 0.5f, b.center.z);
            float t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t / duracion));
                yield return null;
            }
            UbicarEn(banco);
            fuente.PlayOneShot(Clip, 0.8f);
        }

        public void Recoger()
        {
            recogida = true;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (recogida || GestorJuego.EstaPausado) return;

            // Brillito que titila (como un reflejo de la luz en el metal)
            brillo.intensity = intensidadLuz * (0.8f + 0.2f * Mathf.Sin(Time.time * 1.5f)); // pulso suave
            if (!Colocada) return; // todavía la tiene Gonza

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Random.Range(intervalo.x, intervalo.y);
            fuente.pitch = Random.Range(0.92f, 1.1f);
            fuente.PlayOneShot(Clip, volumen);
            MostrarPista();
        }

        void MostrarPista()
        {
            if (!mostrarPistaEnPantalla || cam == null || HUD.I == null) return;
            var j = cam.GetComponentInParent<ControladorJugador>();
            if (j != null && (j.Sentado || j.BloquearMovimiento)) return;

            Vector3 d = transform.position - cam.position;
            float dist = d.magnitude;
            if (dist > alcance) return;

            if (dist <= distanciaMuyCerca)
            {
                HUD.I.Aviso("¡El tintineo está acá nomás! Agachate y revisá los bancos de al lado.", 2f);
                return;
            }
            string cerca = dist <= distanciaCerca ? "Escuchás un tintineo cerca" : "Escuchás un tintineo lejano";
            HUD.I.Aviso($"{cerca}, {Direccion(d)}...", 2f);
        }

        string Direccion(Vector3 haciaLlave)
        {
            Vector3 local = cam.InverseTransformDirection(haciaLlave);
            float angulo = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(angulo) <= 45f) return "adelante";
            if (Mathf.Abs(angulo) >= 135f) return "atrás tuyo";
            return angulo > 0f ? "a tu derecha" : "a tu izquierda";
        }
    }
}
