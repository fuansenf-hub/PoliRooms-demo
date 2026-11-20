using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// "Animación" para modelos sin esqueleto (OBJ de Meshy): respiración en reposo y
    /// balanceo/rebote al caminar, según cuánto se mueve el personaje.
    /// Va en el objeto del modelo (hijo de "Visual"). Cuando lleguen los modelos riggeados
    /// se reemplaza por un Animator.
    /// </summary>
    public class AnimacionProcedural : MonoBehaviour
    {
        [Tooltip("Objeto que se mueve (la raíz del personaje).")]
        public Transform raizMovimiento;
        public float altura = 2.7f;

        [Header("Caminar")]
        [Tooltip("Rebote vertical, como fracción de la altura.")]
        public float rebote = 0.018f;
        public float metrosPorPaso = 1.4f;
        public float balanceoGrados = 3.5f;
        public float inclinacionGrados = 4f;
        public float velocidadCaminarRef = 2.5f;

        [Header("Reposo")]
        public float respiracion = 0.012f;
        public float vaivenGrados = 1.2f;

        Vector3 posBase, escalaBase;
        Quaternion rotBase;
        Vector3 ultimaPos;
        float fase, velSuave, tiempo;

        public float Caminar01 { get; private set; }

        void Start()
        {
            posBase = transform.localPosition;
            rotBase = transform.localRotation;
            escalaBase = transform.localScale;
            if (raizMovimiento == null) raizMovimiento = transform.parent != null && transform.parent.parent != null ? transform.parent.parent : transform;
            ultimaPos = raizMovimiento.position;
            tiempo = Random.value * 10f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 d = raizMovimiento.position - ultimaPos; d.y = 0f;
            ultimaPos = raizMovimiento.position;
            float v = d.magnitude / dt;
            if (v > 30f) v = 0f; // teletransporte
            velSuave = Mathf.Lerp(velSuave, v, dt * 8f);
            Caminar01 = Mathf.Clamp01(velSuave / velocidadCaminarRef);
            fase += d.magnitude / Mathf.Max(0.1f, metrosPorPaso) * Mathf.PI;
            tiempo += dt;

            float reposo = 1f - Caminar01;
            float bote = Mathf.Abs(Mathf.Sin(fase)) * rebote * altura * Caminar01;
            float rol = Mathf.Sin(fase) * balanceoGrados * Caminar01;
            float vaiven = Mathf.Sin(tiempo * 0.7f) * vaivenGrados * reposo;
            float resp = Mathf.Sin(tiempo * 1.8f) * respiracion * reposo;

            transform.localPosition = posBase + Vector3.up * bote;
            transform.localRotation = rotBase * Quaternion.Euler(inclinacionGrados * Caminar01, vaiven, rol);
            transform.localScale = new Vector3(escalaBase.x, escalaBase.y * (1f + resp), escalaBase.z);
        }
    }
}
