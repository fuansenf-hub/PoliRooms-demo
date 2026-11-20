using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// Pupitre que se puede revisar por abajo (agachado, manteniendo E).
    /// Uno de todos los bancos del aula tiene la llave del candado de la gaveta.
    /// </summary>
    public class BancoRevisable : MonoBehaviour, IInteractuable
    {
        public bool esBancoDelJugador;
        public float tiempoRevisar = 1.0f;

        [HideInInspector] public bool tieneLlave;
        [HideInInspector] public LlaveEscondida llave;

        public bool Revisado { get; private set; }

        public event System.Action<BancoRevisable> AlEncontrarLlave;

        static readonly string[] nadaEncontrado =
        {
            "Nada. Solo un chicle pegado.",
            "Nada... un machete de la prueba de química.",
            "Nada. Mugre y más chicles.",
            "Nada. Alguien escribió \"Marinelli te vigila\".",
            "Nada por acá."
        };

        // Tu propio banco no se revisa: sirve para sentarte (E) y hacerte el que estudia.
        public float TiempoMantener => esBancoDelJugador ? 0f : tiempoRevisar;

        public bool PuedeInteractuar(ControladorJugador j) =>
            esBancoDelJugador ? AsientoPropio.PuedeSentarse() : !Revisado && j.Agachado;

        public string TextoInteraccion(ControladorJugador j)
        {
            if (esBancoDelJugador) return AsientoPropio.Texto();
            if (Revisado) return "Ya revisaste este banco";
            if (!j.Agachado) return "Agachate (C) para revisar abajo del banco";
            return "Mantené E — Revisar abajo del banco";
        }

        public void Interactuar(ControladorJugador j)
        {
            if (esBancoDelJugador) { AsientoPropio.Sentarse(); return; }
            Revisado = true;
            if (tieneLlave)
            {
                AudioSource.PlayClipAtPoint(llave != null ? llave.Clip : AudioProcedural.Tintineo, transform.position, 1f);
                if (llave != null) llave.Recoger();
                if (Inventario.Instancia != null) Inventario.Instancia.Agregar(Inventario.LlaveGaveta);
                if (HUD.I != null) HUD.I.Aviso("¡Encontraste la llave del candado!", 2.5f);
                AlEncontrarLlave?.Invoke(this);
            }
            else if (HUD.I != null)
            {
                HUD.I.Aviso(nadaEncontrado[Random.Range(0, nadaEncontrado.Length)], 2f);
            }
        }

        /// <summary>Centro del banco en el mundo (según sus renderers).</summary>
        public Bounds Limites()
        {
            var rs = GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(transform.position, Vector3.one * 0.5f);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
