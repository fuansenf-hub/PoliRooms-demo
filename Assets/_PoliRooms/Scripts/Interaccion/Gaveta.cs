using System.Collections;
using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// Gaveta de aluminio colgada en la pared donde Gonza guarda los celulares.
    /// Se cierra con candado; se abre con la llave escondida abajo de un banco.
    /// </summary>
    public class Gaveta : MonoBehaviour, IInteractuable, IInteractuableMantenido
    {
        public Transform bisagraTapa;
        public float anguloAbierta = -100f;
        public GameObject candado;
        public GameObject celularAdentro;
        public AudioSource fuenteAudio;
        [Tooltip("Llave entrando en el candado (si falta, se usa uno generado).")]
        public AudioClip clipLlaveCandado;
        public float tiempoAbrir = 1.5f;

        public bool Cerrada { get; private set; }
        public bool Vaciada { get; private set; }

        public event System.Action AlAbrir;

        Quaternion rotCerrada;

        void Awake()
        {
            if (bisagraTapa != null) rotCerrada = bisagraTapa.localRotation;
        }

        /// <summary>Gonza guarda el celular y cierra con candado.</summary>
        public void GuardarCelularYCerrar()
        {
            if (celularAdentro != null) celularAdentro.SetActive(true);
            StartCoroutine(MoverTapa(false, 0.6f));
            if (candado != null) candado.SetActive(true);
            if (fuenteAudio != null)
            {
                fuenteAudio.PlayOneShot(AudioProcedural.Bisagra, 0.8f);
                fuenteAudio.PlayOneShot(AudioProcedural.Candado);
            }
            Cerrada = true;
        }

        public float TiempoMantener => tiempoAbrir;

        public bool PuedeInteractuar(ControladorJugador j) =>
            Cerrada && !Vaciada && Inventario.Instancia != null && Inventario.Instancia.Tiene(Inventario.LlaveGaveta);

        public string TextoInteraccion(ControladorJugador j)
        {
            if (Vaciada) return "La gaveta está vacía";
            if (!Cerrada) return "Gaveta de celulares";
            if (Inventario.Instancia == null || !Inventario.Instancia.Tiene(Inventario.LlaveGaveta))
                return "Cerrada con candado. La llave tiene que estar abajo de algún banco...";
            return "Mantené E — Abrir el candado y sacar tu celular";
        }

        // Mientras mantenés E suena la llave entrando y girando en el candado.
        public void AlEmpezarMantener()
        {
            if (fuenteAudio == null || clipLlaveCandado == null) return;
            fuenteAudio.clip = clipLlaveCandado;
            fuenteAudio.time = 0f;
            fuenteAudio.Play();
        }

        public void AlCancelarMantener()
        {
            if (fuenteAudio != null && fuenteAudio.clip == clipLlaveCandado) fuenteAudio.Stop();
        }

        public void Interactuar(ControladorJugador j)
        {
            Vaciada = true;
            if (candado != null) candado.SetActive(false);
            if (fuenteAudio != null)
            {
                fuenteAudio.PlayOneShot(AudioProcedural.Candado);
                fuenteAudio.PlayOneShot(AudioProcedural.Bisagra, 0.8f);
            }
            if (SistemaRuido.Instancia != null) SistemaRuido.Instancia.Pulso(8f, 0.5f); // el candado hace ruido
            StartCoroutine(MoverTapa(true, 0.4f));
            if (celularAdentro != null) celularAdentro.SetActive(false);
            if (Inventario.Instancia != null)
            {
                Inventario.Instancia.Quitar(Inventario.LlaveGaveta);
                Inventario.Instancia.Agregar(Inventario.CelularPro);
            }
            AlAbrir?.Invoke();
        }

        IEnumerator MoverTapa(bool abrir, float duracion)
        {
            if (bisagraTapa == null) yield break;
            Quaternion desde = bisagraTapa.localRotation;
            Quaternion hasta = abrir ? rotCerrada * Quaternion.Euler(0f, anguloAbierta, 0f) : rotCerrada;
            float t = 0f;
            while (t < duracion)
            {
                t += Time.deltaTime;
                bisagraTapa.localRotation = Quaternion.Slerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t / duracion));
                yield return null;
            }
            bisagraTapa.localRotation = hasta;
        }

        /// <summary>Deja la tapa abierta sin animación (estado inicial).</summary>
        public void AbrirTapaInstantaneo()
        {
            if (bisagraTapa != null) bisagraTapa.localRotation = rotCerrada * Quaternion.Euler(0f, anguloAbierta, 0f);
        }
    }
}
