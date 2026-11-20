using UnityEngine;

namespace PoliRooms
{
    /// <summary>
    /// Sonidos cortos generados por código (no hace falta conseguir archivos de audio).
    /// Cuando tengan sonidos reales, se reemplazan acá.
    /// </summary>
    public static class AudioProcedural
    {
        const int Sr = 44100;
        static AudioClip tintineo, candado, golpe, flash, puertaAbrir, puertaCerrar, paso, silla, lapicera, latido, susto, bisagra;

        static AudioClip Crear(string nombre, float duracion, System.Func<float, System.Random, float> f)
        {
            int n = Mathf.CeilToInt(duracion * Sr);
            var datos = new float[n];
            var rnd = new System.Random(nombre.GetHashCode());
            for (int i = 0; i < n; i++) datos[i] = Mathf.Clamp(f(i / (float)Sr, rnd), -1f, 1f);
            // fade-out final para evitar clicks
            int fade = Mathf.Min(n, Sr / 100);
            for (int i = 0; i < fade; i++) datos[n - 1 - i] *= i / (float)fade;
            var clip = AudioClip.Create(nombre, n, 1, Sr, false);
            clip.SetData(datos, 0);
            return clip;
        }

        static float Ruido(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);
        static float Seno(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Env(float t, float inicio, float decaimiento) => t < inicio ? 0f : Mathf.Exp(-(t - inicio) * decaimiento);

        static float Tink(float t)
        {
            if (t < 0f) return 0f;
            float env = Mathf.Exp(-t * 28f);
            return env * (Seno(3150f, t) + 0.6f * Seno(4720f, t) + 0.3f * Seno(6230f, t)) * 0.45f;
        }

        /// <summary>Tintineo metálico de llaves (muy corto).</summary>
        public static AudioClip Tintineo => tintineo ??= Crear("Tintineo", 0.45f,
            (t, r) => Tink(t) + 0.7f * Tink(t - 0.11f) + 0.4f * Tink(t - 0.19f));

        /// <summary>Click de candado / cerradura.</summary>
        public static AudioClip Candado => candado ??= Crear("Candado", 0.3f,
            (t, r) => Ruido(r) * Env(t, 0f, 90f) * 0.6f + Seno(950f, t) * Env(t, 0f, 35f) * 0.5f
                    + Ruido(r) * Env(t, 0.08f, 120f) * 0.5f);

        /// <summary>Piña: golpe grave y seco.</summary>
        public static AudioClip Golpe => golpe ??= Crear("Golpe", 0.6f,
            (t, r) => Mathf.Sin(2f * Mathf.PI * (55f + 70f * Mathf.Exp(-t * 18f)) * t) * Mathf.Exp(-t * 7f)
                    + Ruido(r) * Env(t, 0f, 35f) * 0.6f);

        /// <summary>Click + zumbido del flash de un celular.</summary>
        public static AudioClip Flash => flash ??= Crear("Flash", 0.35f,
            (t, r) => Ruido(r) * Env(t, 0f, 70f) * 0.4f + Seno(2400f, t) * Env(t, 0f, 14f) * 0.15f);

        /// <summary>Puerta que se abre: picaporte + crujido de bisagra.</summary>
        public static AudioClip PuertaAbrir
        {
            get
            {
                if (puertaAbrir != null) return puertaAbrir;
                float fase = 0f, lp = 0f;
                puertaAbrir = Crear("PuertaAbrir", 1.4f, (t, r) =>
                {
                    float picaporte = Ruido(r) * Env(t, 0f, 140f) * 0.7f + Seno(1300f, t) * Env(t, 0f, 60f) * 0.3f
                                    + Ruido(r) * Env(t, 0.09f, 160f) * 0.5f;
                    // crujido: tren de "clicks" con frecuencia variable
                    float frec = 70f + 55f * Mathf.Sin(t * 5.3f) + 25f * Mathf.Sin(t * 13f);
                    fase += frec / Sr;
                    float pulso = (fase % 1f) < 0.08f ? 1f : 0f;
                    lp += (pulso * Ruido(r) - lp) * 0.35f;
                    float envCruj = t < 0.2f ? 0f : Mathf.Sin(Mathf.Clamp01((t - 0.2f) / 1.15f) * Mathf.PI);
                    return picaporte + lp * envCruj * 1.6f;
                });
                return puertaAbrir;
            }
        }

        /// <summary>Puerta que se cierra: golpe sordo + picaporte.</summary>
        public static AudioClip PuertaCerrar => puertaCerrar ??= Crear("PuertaCerrar", 0.7f,
            (t, r) => Seno(65f, t) * Env(t, 0f, 10f) * 0.9f + Ruido(r) * Env(t, 0f, 40f) * 0.5f
                    + Ruido(r) * Env(t, 0.12f, 150f) * 0.5f + Seno(1100f, t) * Env(t, 0.12f, 70f) * 0.25f);

        /// <summary>Paso sobre cerámica.</summary>
        public static AudioClip Paso
        {
            get
            {
                if (paso != null) return paso;
                float lp = 0f;
                paso = Crear("Paso", 0.2f, (t, r) =>
                {
                    lp += (Ruido(r) - lp) * 0.25f;
                    return lp * Env(t, 0f, 35f) * 1.2f + Seno(95f, t) * Env(t, 0f, 30f) * 0.5f
                         + Ruido(r) * Env(t, 0f, 220f) * 0.25f;
                });
                return paso;
            }
        }

        /// <summary>Silla que se arrastra al levantarse.</summary>
        public static AudioClip Silla
        {
            get
            {
                if (silla != null) return silla;
                float lp = 0f, prev = 0f;
                silla = Crear("Silla", 0.6f, (t, r) =>
                {
                    float n = Ruido(r);
                    lp += (n - lp) * 0.15f;
                    float banda = lp - prev; prev = lp;
                    float am = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 38f * t);
                    float env = Mathf.Sin(Mathf.Clamp01(t / 0.55f) * Mathf.PI);
                    return banda * am * env * 6f;
                });
                return silla;
            }
        }

        /// <summary>Lapicera rayando papel (Mayra corrigiendo).</summary>
        public static AudioClip Lapicera
        {
            get
            {
                if (lapicera != null) return lapicera;
                float prev = 0f;
                lapicera = Crear("Lapicera", 0.4f, (t, r) =>
                {
                    float n = Ruido(r);
                    float hp = n - prev; prev = n;
                    float trazos = Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 7.5f * t));
                    return hp * trazos * 0.25f;
                });
                return lapicera;
            }
        }

        /// <summary>Latido (lub-dub).</summary>
        public static AudioClip Latido => latido ??= Crear("Latido", 0.55f,
            (t, r) => Seno(52f, t) * Env(t, 0f, 18f) * 0.9f + Seno(48f, t) * Env(t, 0.2f, 22f) * 0.7f);

        /// <summary>Golpe de tensión cuando un profe te ve.</summary>
        public static AudioClip Susto => susto ??= Crear("Susto", 1.4f,
            (t, r) => Seno(38f, t) * Env(t, 0f, 2.5f) * 0.8f
                    + (Seno(1290f, t) + Seno(1371f, t) + Seno(1830f, t)) * Env(t, 0.02f, 3.2f) * 0.12f
                    + Ruido(r) * Env(t, 0f, 9f) * 0.3f);

        /// <summary>Chirrido corto de bisagra (tapa de la gaveta).</summary>
        public static AudioClip Bisagra => bisagra ??= Crear("Bisagra", 0.5f,
            (t, r) => Mathf.Sin(2f * Mathf.PI * (850f + 420f * Mathf.Sin(2f * Mathf.PI * 2.2f * t)) * t)
                    * Mathf.Sin(Mathf.Clamp01(t / 0.48f) * Mathf.PI) * 0.18f);
    }
}
