using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// Maneja el Animator de un personaje humanoide (ControladorPersonaje):
    ///  - "Velocidad": se calcula sola según cuánto se mueve el personaje.
    ///  - "Accion": qué conjunto de animaciones usar (ver constantes).
    /// </summary>
    public class AnimadorPersonaje : MonoBehaviour
    {
        // Debe coincidir con el controlador que arma ConstructorNivel1.
        public const int Locomocion = 0;      // quieto / caminar
        public const int Sigilo = 1;          // caminar en puntas de pie
        public const int Escribiendo = 2;     // sentada corrigiendo
        public const int SentadaMirando = 3;  // sentada, levanta la vista con desaprobación
        public const int Levantarse = 4;      // se para de la silla
        public const int Pesado = 5;          // caminar "pesado" (preceptor)
        public const int Sentado = 6;         // sentado quieto
        public const int Agachado = 7;        // caminar agachado

        public Animator animator;
        public int accionInicial = Locomocion;

        public int Accion { get; private set; } = -1;

        static readonly int HashVelocidad = Animator.StringToHash("Velocidad");
        static readonly int HashAccion = Animator.StringToHash("Accion");

        Vector3 ultimaPos;
        float velSuave;

        void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            ultimaPos = transform.position;
            if (Accion < 0) SetAccion(accionInicial);
        }

        public void SetAccion(int accion)
        {
            if (Accion == accion) return;
            Accion = accion;
            if (animator != null) animator.SetInteger(HashAccion, accion);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || animator == null) return;
            Vector3 d = transform.position - ultimaPos; d.y = 0f;
            ultimaPos = transform.position;
            float v = d.magnitude / dt;
            if (v > 30f) v = 0f; // teletransporte
            velSuave = Mathf.Lerp(velSuave, v, dt * 10f);
            animator.SetFloat(HashVelocidad, velSuave < 0.15f ? 0f : velSuave);
        }
    }
}
