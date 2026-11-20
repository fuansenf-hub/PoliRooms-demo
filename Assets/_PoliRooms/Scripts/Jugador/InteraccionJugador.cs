using UnityEngine;

namespace PoliRooms
{
    /// <summary>Cualquier cosa con la que se interactúa con E.</summary>
    public interface IInteractuable
    {
        /// <summary>Texto del cartel (también se muestra si no se puede, como pista).</summary>
        string TextoInteraccion(ControladorJugador jugador);
        bool PuedeInteractuar(ControladorJugador jugador);
        /// <summary>Segundos que hay que mantener E (0 = tocar una vez).</summary>
        float TiempoMantener { get; }
        void Interactuar(ControladorJugador jugador);
    }

    /// <summary>Opcional: para reaccionar cuando empezás / soltás a mitad un "mantener E".</summary>
    public interface IInteractuableMantenido
    {
        void AlEmpezarMantener();
        void AlCancelarMantener();
    }

    /// <summary>
    /// Raycast desde la cámara para encontrar objetos interactuables, mostrar el cartel y
    /// manejar "tocar E" o "mantener E".
    /// </summary>
    [RequireComponent(typeof(ControladorJugador))]
    public class InteraccionJugador : MonoBehaviour
    {
        public float distancia = 3.6f;
        public LayerMask capas = ~(1 << 2); // todo menos "Ignore Raycast"

        ControladorJugador jugador;
        IInteractuable actual;
        float progreso;

        void Awake() => jugador = GetComponent<ControladorJugador>();

        void Update()
        {
            if (GestorJuego.EstaPausado) return;

            IInteractuable objetivo = null;
            if (!jugador.BloquearMovimiento && !jugador.Sentado && !GestorJuego.EstaTerminado)
            {
                Transform cam = jugador.camara;
                if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, distancia, capas, QueryTriggerInteraction.Collide))
                    objetivo = hit.collider.GetComponentInParent<IInteractuable>();
            }

            if (objetivo != actual)
            {
                CancelarMantener();
                actual = objetivo;
            }

            if (actual == null)
            {
                if (HUD.I != null) HUD.I.Prompt(null, false, 0f);
                return;
            }

            bool puede = actual.PuedeInteractuar(jugador);
            if (puede && actual.TiempoMantener <= 0f)
            {
                if (ControlesJuego.Interactuar.WasPressedThisFrame()) actual.Interactuar(jugador);
            }
            else if (puede && ControlesJuego.Interactuar.IsPressed())
            {
                if (!manteniendo)
                {
                    manteniendo = true;
                    (actual as IInteractuableMantenido)?.AlEmpezarMantener();
                }
                progreso += Time.deltaTime / actual.TiempoMantener;
                if (progreso >= 1f)
                {
                    progreso = 0f;
                    manteniendo = false;
                    actual.Interactuar(jugador);
                }
            }
            else CancelarMantener();

            if (HUD.I != null) HUD.I.Prompt(actual.TextoInteraccion(jugador), puede, progreso);
        }

        bool manteniendo;

        void CancelarMantener()
        {
            if (manteniendo && actual is IInteractuableMantenido m) m.AlCancelarMantener();
            manteniendo = false;
            progreso = 0f;
        }

        void OnDisable() => CancelarMantener();
    }
}
