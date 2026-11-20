using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// Medidor de cordura. Baja mientras los profes te persiguen o te buscan.
    /// En 0 el jugador muere. Debajo de 'umbralBlur' la vista se empieza a nublar (ver EfectosCamara).
    /// El valor con el que se entra a un nivel se conserva entre niveles y al reiniciar.
    /// </summary>
    public class Cordura : MonoBehaviour
    {
        public static Cordura Instancia { get; private set; }

        /// <summary>Cordura con la que arranca el nivel actual (se usa al reiniciar).</summary>
        public static float ValorAlEntrarNivel = 100f;

        public float maximo = 100f;
        [Range(0f, 1f)] public float umbralBlur = 0.3f;

        public float Valor { get; private set; }
        public float Normalizada => maximo > 0f ? Valor / maximo : 0f;

        float drenajeEsteFrame;

        void Awake()
        {
            Instancia = this;
            Valor = Mathf.Clamp(ValorAlEntrarNivel, 1f, maximo);
        }

        /// <summary>Pide drenar cordura este frame. Si varias fuentes piden, se usa la mayor.</summary>
        public void Drenar(float porSegundo)
        {
            if (porSegundo > drenajeEsteFrame) drenajeEsteFrame = porSegundo;
        }

        void LateUpdate()
        {
            if (drenajeEsteFrame > 0f && !GestorJuego.EstaTerminado)
            {
                Valor -= drenajeEsteFrame * Time.deltaTime;
                if (Valor <= 0f)
                {
                    Valor = 0f;
                    if (GestorJuego.Instancia != null) GestorJuego.Instancia.Morir("Perdiste la cordura.");
                }
            }
            drenajeEsteFrame = 0f;
        }

        public void GuardarParaSiguienteNivel() => ValorAlEntrarNivel = Valor;
    }
}
