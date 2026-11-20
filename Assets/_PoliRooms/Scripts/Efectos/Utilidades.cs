using System.Collections;
using UnityEngine;

namespace PoliRooms
{
    /// <summary>Mueve personajes "estatua" (sin animación) por una lista de puntos.</summary>
    public static class MovimientoSimple
    {
        public static IEnumerator Recorrer(Transform t, Vector3[] puntos, float velocidad, float pausaEnCadaPunto = 0f)
        {
            foreach (var p in puntos)
            {
                Vector3 destino = new Vector3(p.x, t.position.y, p.z);
                while ((t.position - destino).sqrMagnitude > 0.0025f)
                {
                    Vector3 d = destino - t.position;
                    if (d.sqrMagnitude > 0.0001f)
                        t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(d), Time.deltaTime * 8f);
                    t.position = Vector3.MoveTowards(t.position, destino, velocidad * Time.deltaTime);
                    yield return null;
                }
                if (pausaEnCadaPunto > 0f) yield return new WaitForSeconds(pausaEnCadaPunto);
            }
        }

        public static IEnumerator GirarHacia(Transform t, Vector3 punto, float duracion)
        {
            Vector3 d = punto - t.position; d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) yield break;
            Quaternion desde = t.rotation, hasta = Quaternion.LookRotation(d);
            float k = 0f;
            while (k < duracion)
            {
                k += Time.deltaTime;
                t.rotation = Quaternion.Slerp(desde, hasta, k / duracion);
                yield return null;
            }
        }
    }
}
