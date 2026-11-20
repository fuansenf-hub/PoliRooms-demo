using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PoliRooms
{
    /// <summary>
    /// Estado general de la partida: pausa, muerte y reinicio del nivel.
    /// </summary>
    public class GestorJuego : MonoBehaviour
    {
        public static GestorJuego Instancia { get; private set; }

        public bool Pausado { get; private set; }
        public bool Terminado { get; private set; }

        /// <summary>True si el jugador ya murió al menos una vez en este nivel (permite saltear la intro).</summary>
        public static bool MurioEnEsteNivel;

        public static bool EstaPausado => Instancia != null && Instancia.Pausado;
        public static bool EstaTerminado => Instancia != null && Instancia.Terminado;

        void Awake()
        {
            Instancia = this;
            ControlesJuego.Asegurar();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            if (!Terminado && ControlesJuego.Pausa.WasPressedThisFrame())
                CambiarPausa(!Pausado);

            if (Pausado && ControlesJuego.Reiniciar.WasPressedThisFrame())
                ReiniciarNivel();
        }

        public void CambiarPausa(bool pausar)
        {
            Pausado = pausar;
            Time.timeScale = pausar ? 0f : 1f;
            AudioListener.pause = pausar;
            Cursor.lockState = pausar ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = pausar;
            if (HUD.I != null) HUD.I.MostrarPausa(pausar);
        }

        public void ReiniciarNivel()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>El jugador perdió: fundido, mensaje y reinicio del nivel.</summary>
        public void Morir(string motivo)
        {
            if (Terminado) return;
            Terminado = true;
            MurioEnEsteNivel = true;
            StartCoroutine(RutinaMuerte(motivo));
        }

        /// <summary>Marca la partida como terminada sin reiniciar (fin de nivel).</summary>
        public void MarcarTerminado() => Terminado = true;

        IEnumerator RutinaMuerte(string motivo)
        {
            if (HUD.I != null)
            {
                HUD.I.Fundir(1f, 1.2f);
                yield return new WaitForSecondsRealtime(1.2f);
                HUD.I.MensajeCentral(motivo, "Reiniciando el nivel...");
            }
            yield return new WaitForSecondsRealtime(2.5f);
            ReiniciarNivel();
        }
    }
}
