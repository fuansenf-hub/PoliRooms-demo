using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// Para personajes con esqueleto: prende el parámetro "Caminando" del Animator cuando
    /// el personaje se mueve (idle ↔ caminar), aunque lo mueva un script y no un NavMeshAgent.
    /// </summary>
    public class AnimadorPorVelocidad : MonoBehaviour
    {
        public Animator animator;
        public string parametroCaminando = "Caminando";
        public float umbral = 0.25f;

        Vector3 ultimaPos;
        float velSuave;
        int hash;

        void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            hash = Animator.StringToHash(parametroCaminando);
            ultimaPos = transform.position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || animator == null) return;
            Vector3 d = transform.position - ultimaPos; d.y = 0f;
            ultimaPos = transform.position;
            float v = d.magnitude / dt;
            if (v > 30f) v = 0f;
            velSuave = Mathf.Lerp(velSuave, v, dt * 10f);
            animator.SetBool(hash, velSuave > umbral);
        }
    }
}
