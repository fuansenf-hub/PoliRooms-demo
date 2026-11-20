using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// Tu silla: con E te sentás y te hacés el que estudia. Si Mayra te estaba buscando,
    /// se le pasa y vuelve a corregir. (Tu pupitre hace lo mismo, ver BancoRevisable.)
    /// </summary>
    public class AsientoPropio : MonoBehaviour, IInteractuable
    {
        public float TiempoMantener => 0f;
        public bool PuedeInteractuar(ControladorJugador j) => PuedeSentarse();
        public string TextoInteraccion(ControladorJugador j) => Texto();
        public void Interactuar(ControladorJugador j) => Sentarse();

        static DirectorNivel1 Director => DirectorNivel1.Instancia;

        public static bool PuedeSentarse() => Director != null && Director.PuedeSentarse;

        public static string Texto()
        {
            if (Director == null) return null;
            if (Director.YaNoSirveSentarse) return "Ya no sirve hacerte el que estudia: ¡escapá!";
            if (!Director.PuedeSentarse) return null;
            return Director.MayraTeBusca
                ? "E — Sentarte en tu banco (hacete el que estudia)"
                : "E — Sentarte en tu banco";
        }

        public static void Sentarse()
        {
            if (Director != null) Director.SentarseEnBanco();
        }
    }
}
