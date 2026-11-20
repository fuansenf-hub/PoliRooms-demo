using System.Collections;
using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// Puerta de la pecera. No tiene llave: el obstáculo es Mayra.
    /// Solo te vas si ya recuperaste tu celular.
    /// </summary>
    public class PuertaAula : MonoBehaviour, IInteractuable
    {
        public Transform bisagra;
        [Tooltip("Ángulo (en Y) que gira la bisagra al abrirse.")]
        public float anguloAbierta = 95f;
        public AudioSource fuenteAudio;
        [Tooltip("Sonido real de la puerta abriéndose (si falta, se usa uno generado).")]
        public AudioClip clipAbrir;
        public float tiempoAbrir = 0.6f;

        public bool Abierta { get; private set; }
        public event System.Action AlAbrir;

        public float TiempoMantener => tiempoAbrir;

        public bool PuedeInteractuar(ControladorJugador j) =>
            !Abierta && Inventario.Instancia != null && Inventario.Instancia.Tiene(Inventario.CelularPro);

        public string TextoInteraccion(ControladorJugador j)
        {
            if (Abierta) return null;
            if (Inventario.Instancia == null || !Inventario.Instancia.Tiene(Inventario.CelularPro))
                return "No me voy sin mi celular.";
            return "Mantené E — Abrir la puerta";
        }

        public void Interactuar(ControladorJugador j) => Abrir(true);

        public void Abrir(bool conSonido)
        {
            if (Abierta) return;
            Abierta = true;
            if (conSonido && fuenteAudio != null) fuenteAudio.PlayOneShot(clipAbrir != null ? clipAbrir : AudioProcedural.PuertaAbrir, 1f);
            if (SistemaRuido.Instancia != null && conSonido) SistemaRuido.Instancia.Pulso(6f, 0.6f);
            StartCoroutine(Girar(anguloAbierta, 1.1f, true));
            AlAbrir?.Invoke();
        }

        public void Cerrar()
        {
            if (!Abierta) return;
            Abierta = false;
            StartCoroutine(CerrarConGolpe());
        }

        IEnumerator CerrarConGolpe()
        {
            yield return Girar(0f, 0.7f, false);
            if (fuenteAudio != null) fuenteAudio.PlayOneShot(AudioProcedural.PuertaCerrar, 1f);
        }

        IEnumerator Girar(float angulo, float duracion, bool abriendo)
        {
            if (bisagra == null) yield break;
            // Mientras está abierta, la hoja no bloquea al jugador.
            foreach (var c in bisagra.GetComponentsInChildren<Collider>()) c.enabled = !abriendo;
            Quaternion desde = bisagra.localRotation;
            Quaternion hasta = Quaternion.Euler(0f, angulo, 0f);
            float t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                bisagra.localRotation = Quaternion.Slerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t / duracion));
                yield return null;
            }
            bisagra.localRotation = hasta;
        }
    }
}
