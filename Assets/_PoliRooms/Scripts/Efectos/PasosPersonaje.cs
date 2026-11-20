using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// Pasos para personajes movidos por script (Gonza, preceptor).
    /// Con un clip real en loop suena mientras camina; si no hay clip, usa pasos generados.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PasosPersonaje : MonoBehaviour
    {
        public AudioClip clipLoop;
        public float metrosPorPaso = 1.4f;
        [Range(0f, 1f)] public float volumen = 0.45f;
        public float velocidadReferencia = 2.4f;

        AudioSource fuente;
        Vector3 ultimaPos;
        float acumulado, velSuave;

        void Start()
        {
            fuente = GetComponent<AudioSource>();
            fuente.playOnAwake = false;
            fuente.spatialBlend = 1f;
            fuente.maxDistance = 25f;
            fuente.rolloffMode = AudioRolloffMode.Linear;
            if (clipLoop != null) { fuente.clip = clipLoop; fuente.loop = true; fuente.volume = volumen; }
            ultimaPos = transform.position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 d = transform.position - ultimaPos; d.y = 0f;
            ultimaPos = transform.position;
            if (d.magnitude > 3f) return; // teletransporte
            velSuave = Mathf.Lerp(velSuave, d.magnitude / dt, dt * 10f);

            if (clipLoop != null)
            {
                if (velSuave > 0.3f)
                {
                    fuente.pitch = Mathf.Clamp(velSuave / velocidadReferencia, 0.8f, 1.5f);
                    if (!fuente.isPlaying) fuente.Play();
                }
                else if (fuente.isPlaying) fuente.Pause();
                return;
            }

            acumulado += d.magnitude;
            if (acumulado < metrosPorPaso) return;
            acumulado = 0f;
            fuente.pitch = Random.Range(0.85f, 1.05f);
            fuente.PlayOneShot(AudioProcedural.Paso, volumen);
        }
    }
}
