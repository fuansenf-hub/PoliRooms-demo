using UnityEngine;

namespace PoliRooms
{
    /// <summary>Tubo fluorescente que parpadea de vez en cuando.</summary>
    [RequireComponent(typeof(Light))]
    public class LuzParpadeante : MonoBehaviour
    {
        public Vector2 intervalo = new Vector2(2f, 7f);
        Light luz;
        float intensidadBase, timer;
        int parpadeos;

        void Awake()
        {
            luz = GetComponent<Light>();
            intensidadBase = luz.intensity;
            timer = Random.Range(intervalo.x, intervalo.y);
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            if (parpadeos <= 0) parpadeos = Random.Range(2, 6);
            luz.intensity = luz.intensity > intensidadBase * 0.5f ? intensidadBase * Random.Range(0f, 0.25f) : intensidadBase;
            parpadeos--;
            timer = parpadeos > 0 ? Random.Range(0.04f, 0.12f) : Random.Range(intervalo.x, intervalo.y);
            if (parpadeos <= 0) luz.intensity = intensidadBase;
        }
    }
}
