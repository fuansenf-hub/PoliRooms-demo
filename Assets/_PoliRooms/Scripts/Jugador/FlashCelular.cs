using System.Collections;
using UnityEngine;

namespace PoliRooms
{
    /// <summary>Algo que se puede encandilar con el flash.</summary>
    public interface IAturdible
    {
        Vector3 PuntoObjetivo { get; }
        void Aturdir(float segundos);
    }

    /// <summary>
    /// Flash del celular con recarga. Hay dos perfiles: el Nokia de tapita (flash leve,
    /// aturde muy poco) y el celular pro (flash fuerte). Si tenés el pro, se usa ese.
    /// </summary>
    public class FlashCelular : MonoBehaviour
    {
        [System.Serializable]
        public class Perfil
        {
            public string nombre;
            public float alcance;
            [Tooltip("Medio ángulo del cono, en grados.")] public float angulo;
            public float aturdimiento;
            public float recarga;
            [Range(0f, 1f)] public float intensidadPantalla;
            public float intensidadLuz;
            public Color color;
        }

        public Perfil nokia = new Perfil
        {
            nombre = "Nokia", alcance = 6.5f, angulo = 20f, aturdimiento = 1.2f, recarga = 6f,
            intensidadPantalla = 0.3f, intensidadLuz = 2.5f, color = new Color(1f, 0.9f, 0.65f)
        };

        public Perfil pro = new Perfil
        {
            nombre = "Celular", alcance = 14f, angulo = 35f, aturdimiento = 5f, recarga = 12f,
            intensidadPantalla = 0.85f, intensidadLuz = 8f, color = Color.white
        };

        public Light luz;
        public AudioSource fuenteAudio;

        float recargaRestante;
        ControladorJugador jugador;

        public bool TienePro => Inventario.Instancia != null && Inventario.Instancia.Tiene(Inventario.CelularPro);
        public bool TieneAlguno => TienePro || (Inventario.Instancia != null && Inventario.Instancia.Tiene(Inventario.Nokia));
        public Perfil Actual => TienePro ? pro : nokia;
        public float Recarga01 => Actual.recarga > 0f ? 1f - Mathf.Clamp01(recargaRestante / Actual.recarga) : 1f;
        public bool Disponible => TieneAlguno && recargaRestante <= 0f;

        /// <summary>Se dispara cada vez que se usa el flash; el bool indica si aturdió a alguien.</summary>
        public event System.Action<Perfil, bool> AlDisparar;

        public static FlashCelular Instancia { get; private set; }

        void Awake()
        {
            Instancia = this;
            jugador = GetComponent<ControladorJugador>();
            if (luz != null) luz.enabled = false;
        }

        void Update()
        {
            if (GestorJuego.EstaPausado) return;
            if (recargaRestante > 0f) recargaRestante -= Time.deltaTime;

            bool bloqueado = jugador != null && (jugador.BloquearMovimiento || jugador.Sentado);
            if (!bloqueado && !GestorJuego.EstaTerminado && ControlesJuego.Flash.WasPressedThisFrame())
            {
                if (Disponible) Disparar();
                else if (TieneAlguno && HUD.I != null) HUD.I.Aviso("El flash se está recargando...", 1.2f);
            }
        }

        public void Disparar()
        {
            Perfil p = Actual;
            recargaRestante = p.recarga;

            if (HUD.I != null) HUD.I.Destello(p.color, p.intensidadPantalla, 0.35f);
            if (fuenteAudio != null) fuenteAudio.PlayOneShot(AudioProcedural.Flash, 0.8f);
            if (luz != null) StartCoroutine(Fogonazo(p));
            if (SistemaRuido.Instancia != null) SistemaRuido.Instancia.Pulso(3f, 0.3f);

            bool acerto = false;
            Transform cam = jugador != null ? jugador.camara : Camera.main.transform;
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(mb is IAturdible objetivo) || !mb.isActiveAndEnabled) continue;
                Vector3 haciaObj = objetivo.PuntoObjetivo - cam.position;
                if (haciaObj.magnitude > p.alcance) continue;
                if (Vector3.Angle(cam.forward, haciaObj) > p.angulo) continue;
                if (Physics.Linecast(cam.position, objetivo.PuntoObjetivo, out RaycastHit hit, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    continue; // hay una pared o mueble en el medio
                objetivo.Aturdir(p.aturdimiento);
                acerto = true;
            }
            AlDisparar?.Invoke(p, acerto);
        }

        IEnumerator Fogonazo(Perfil p)
        {
            luz.color = p.color;
            luz.intensity = p.intensidadLuz;
            luz.range = p.alcance * 1.5f;
            luz.spotAngle = p.angulo * 2.4f;
            luz.enabled = true;
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.deltaTime;
                luz.intensity = Mathf.Lerp(p.intensidadLuz, 0f, t / 0.18f);
                yield return null;
            }
            luz.enabled = false;
        }
    }
}
