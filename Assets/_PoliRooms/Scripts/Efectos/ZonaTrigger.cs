using UnityEngine;

namespace PoliRooms
{
    /// <summary>Trigger que avisa cuando entra el jugador.</summary>
    [RequireComponent(typeof(Collider))]
    public class ZonaTrigger : MonoBehaviour
    {
        public event System.Action AlEntrarJugador;
        bool disparado;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider otro)
        {
            if (disparado || !otro.CompareTag("Player")) return;
            disparado = true;
            AlEntrarJugador?.Invoke();
        }
    }
}
