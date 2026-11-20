using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace PoliRooms
{
    /// <summary>
    /// Post-proceso que reacciona al estado del jugador:
    ///  - Cordura por debajo del umbral (30%): blur (profundidad de campo) y viñeta que crecen hasta morir.
    ///  - Mayra cerca y alerta: aberración cromática, grano, distorsión y estática.
    /// </summary>
    public class EfectosCamara : MonoBehaviour
    {
        public PostProcessVolume volumen;
        public ProfesoraMayra perseguidor;
        public AudioSource audioEstatica;

        [Header("Cercanía del perseguidor")]
        public float distanciaEfecto = 14f;
        public float aberracionMaxima = 1f;
        public float granoMaximo = 0.9f;
        public float distorsionMaxima = -35f;
        public float volumenEstaticaMaximo = 0.5f;

        DepthOfField dof;
        Vignette vineta;
        ChromaticAberration aberracion;
        Grain grano;
        LensDistortion distorsion;
        float intensidadCercania;
        AudioSource audioLatido;
        float timerLatido;

        void Start()
        {
            audioLatido = gameObject.AddComponent<AudioSource>();
            audioLatido.playOnAwake = false;
            audioLatido.spatialBlend = 0f;

            if (volumen == null) volumen = FindFirstObjectByType<PostProcessVolume>();
            if (volumen == null) { enabled = false; return; }
            var perfil = volumen.profile; // instancia propia (no modifica el asset)
            perfil.TryGetSettings(out dof);
            perfil.TryGetSettings(out vineta);
            perfil.TryGetSettings(out aberracion);
            perfil.TryGetSettings(out grano);
            perfil.TryGetSettings(out distorsion);
            if (audioEstatica != null) { audioEstatica.volume = 0f; if (!audioEstatica.isPlaying) audioEstatica.Play(); }
        }

        void Update()
        {
            // --- Cordura ---
            float t = 0f;
            var c = Cordura.Instancia;
            if (c != null && c.Normalizada < c.umbralBlur)
                t = 1f - c.Normalizada / Mathf.Max(0.01f, c.umbralBlur);

            if (dof != null)
            {
                dof.enabled.Override(t > 0.001f);
                dof.focusDistance.Override(Mathf.Lerp(4f, 0.15f, t));
                dof.aperture.Override(Mathf.Lerp(8f, 0.8f, t));
                dof.focalLength.Override(Mathf.Lerp(40f, 200f, t));
            }
            if (vineta != null)
            {
                float pulso = t > 0f ? Mathf.Sin(Time.time * (2f + t * 4f)) * 0.04f * t : 0f;
                vineta.intensity.Override(Mathf.Lerp(0.3f, 0.6f, t) + pulso);
            }

            // --- Cercanía de Mayra ---
            float objetivo = 0f;
            if (perseguidor != null)
            {
                var e = perseguidor.Estado;
                bool alerta = e == EstadoMayra.Persiguiendo || e == EstadoMayra.Buscando || e == EstadoMayra.CuidandoPuerta
                           || e == EstadoMayra.Patrullando || e == EstadoMayra.Atrapando;
                if (alerta)
                {
                    float d = Vector3.Distance(transform.position, perseguidor.transform.position);
                    float k = Mathf.Clamp01(1f - d / distanciaEfecto);
                    objetivo = k * k;
                }
            }
            intensidadCercania = Mathf.Lerp(intensidadCercania, objetivo, Time.deltaTime * 4f);
            float i = intensidadCercania;

            if (aberracion != null) aberracion.intensity.Override(i * aberracionMaxima + t * 0.3f);
            if (grano != null) grano.intensity.Override(0.2f + i * (granoMaximo - 0.2f));
            if (distorsion != null) distorsion.intensity.Override(i * distorsionMaxima);
            if (audioEstatica != null) audioEstatica.volume = i * volumenEstaticaMaximo;

            // --- Latido: más rápido y fuerte con la cordura baja o con Mayra encima ---
            bool persiguiendo = perseguidor != null && perseguidor.Estado == EstadoMayra.Persiguiendo;
            float tension = Mathf.Max(t, persiguiendo ? 0.5f + i * 0.5f : 0f);
            if (tension > 0.02f && audioLatido != null && !GestorJuego.EstaPausado)
            {
                timerLatido -= Time.deltaTime;
                if (timerLatido <= 0f)
                {
                    audioLatido.PlayOneShot(AudioProcedural.Latido, Mathf.Lerp(0.15f, 0.7f, tension));
                    timerLatido = Mathf.Lerp(1.1f, 0.42f, tension);
                }
            }
        }
    }
}
