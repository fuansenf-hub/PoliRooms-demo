using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// Ruido que hace el jugador, expresado como un radio en metros: si un profe está dentro
    /// de ese radio, te escucha. Suma el ruido de moverse (sprint > caminar > agachado) más
    /// el volumen del micrófono (opcional; si no hay micrófono, solo cuenta el movimiento).
    /// </summary>
    public class SistemaRuido : MonoBehaviour
    {
        public static SistemaRuido Instancia { get; private set; }

        [Header("Ruido por movimiento (metros)")]
        public float radioAgachado = 3.2f;
        public float radioCaminando = 9.6f;
        public float radioCorriendo = 22f;

        [Header("Micrófono")]
        public bool usarMicrofono = true;
        [Tooltip("Metros que suma el micrófono cuando está al máximo.")]
        public float radioMaximoMicrofono = 16f;
        [Tooltip("dB por encima del ruido ambiente a partir de los cuales cuenta.")]
        public float umbralDb = 8f;
        [Tooltip("dB por encima del umbral para llegar al máximo.")]
        public float rangoDb = 28f;
        public float segundosCalibracion = 2f;

        public float RadioMovimiento { get; private set; }
        public float RadioMicrofono { get; private set; }
        public float Radio => Mathf.Max(RadioMovimiento, pulsoRadio) + RadioMicrofono;
        public float NivelMicrofono01 { get; private set; }
        public bool MicrofonoActivo { get; private set; }
        public float RadioMaximoPosible => radioCorriendo + radioMaximoMicrofono;

        float pulsoRadio, pulsoTiempo;
        AudioClip clipMic;
        string dispositivo;
        readonly float[] muestras = new float[512];
        float pisoDb = -50f;
        float calibracionRestante;
        const int Frecuencia = 16000;

        void Awake() => Instancia = this;

        void Start()
        {
            if (!usarMicrofono) return;
            try
            {
                if (Microphone.devices.Length == 0) return;
                dispositivo = Microphone.devices[0];
                clipMic = Microphone.Start(dispositivo, true, 1, Frecuencia);
                MicrofonoActivo = clipMic != null;
                calibracionRestante = segundosCalibracion;
                pisoDb = 0f;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[PoliRooms] No se pudo iniciar el micrófono: " + e.Message);
                MicrofonoActivo = false;
            }
        }

        void OnDisable()
        {
            if (MicrofonoActivo && Microphone.IsRecording(dispositivo)) Microphone.End(dispositivo);
            MicrofonoActivo = false;
        }

        /// <summary>Lo llama el controlador del jugador cada frame.</summary>
        public void ReportarMovimiento(float radio) => RadioMovimiento = radio;

        /// <summary>Ruido puntual (levantarse de la silla, el click del flash, etc.).</summary>
        public void Pulso(float radio, float duracion)
        {
            if (radio >= pulsoRadio) { pulsoRadio = radio; pulsoTiempo = duracion; }
        }

        void Update()
        {
            if (pulsoTiempo > 0f)
            {
                pulsoTiempo -= Time.deltaTime;
                if (pulsoTiempo <= 0f) pulsoRadio = 0f;
            }

            if (!MicrofonoActivo) { RadioMicrofono = 0f; NivelMicrofono01 = 0f; return; }

            int pos = Microphone.GetPosition(dispositivo);
            if (pos < muestras.Length) return; // evitamos leer cruzando el inicio del buffer
            clipMic.GetData(muestras, pos - muestras.Length);

            float suma = 0f;
            for (int i = 0; i < muestras.Length; i++) suma += muestras[i] * muestras[i];
            float rms = Mathf.Sqrt(suma / muestras.Length);
            float db = 20f * Mathf.Log10(Mathf.Max(rms, 1e-5f));

            if (calibracionRestante > 0f)
            {
                // Promedio del ruido ambiente durante los primeros segundos.
                pisoDb = pisoDb == 0f ? db : Mathf.Lerp(pisoDb, db, 0.1f);
                calibracionRestante -= Time.deltaTime;
                RadioMicrofono = 0f;
                return;
            }

            // El piso sigue rápido cuando baja y muy lento cuando sube (ruido de fondo constante).
            pisoDb = db < pisoDb ? Mathf.Lerp(pisoDb, db, 0.2f) : pisoDb + 0.25f * Time.deltaTime;

            float nivel = Mathf.Clamp01((db - pisoDb - umbralDb) / rangoDb);
            NivelMicrofono01 = Mathf.Max(nivel, NivelMicrofono01 - Time.deltaTime * 1.2f);
            RadioMicrofono = NivelMicrofono01 * radioMaximoMicrofono;
        }
    }
}
