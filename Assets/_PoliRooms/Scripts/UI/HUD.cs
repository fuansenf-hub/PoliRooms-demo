using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PoliRooms
{
    /// <summary>
    /// HUD completo, armado por código: cordura, ruido, flash, cartel de interacción,
    /// objetivo, subtítulos, avisos, mensajes grandes, fundidos y destellos.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD I { get; private set; }

        Font fuente;
        RectTransform raiz;
        Image barraCordura, barraRuido, barraRuidoMic, barraFlash, barraPrompt, fundido, destello;
        Text textoCordura, textoRuido, textoFlash, textoPrompt, textoObjetivo, textoSubtitulo, textoAviso, textoCentral, textoCentralSub, textoPausa;
        GameObject grupoPrompt, grupoFlash, panelPausa;
        Coroutine rutSub, rutAviso, rutFundido, rutDestello;

        static readonly Color ColorCordura = new Color(0.55f, 0.85f, 0.75f);
        static readonly Color ColorCorduraBaja = new Color(0.9f, 0.25f, 0.2f);

        void Awake()
        {
            I = this;
            fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Construir();
        }

        // ------------------------------------------------------------------ API

        public void Prompt(string texto, bool habilitado, float progreso)
        {
            bool mostrar = !string.IsNullOrEmpty(texto);
            grupoPrompt.SetActive(mostrar);
            if (!mostrar) return;
            textoPrompt.text = texto;
            textoPrompt.color = habilitado ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            barraPrompt.transform.parent.gameObject.SetActive(habilitado && progreso > 0f);
            Llenar(barraPrompt, progreso);
        }

        /// <summary>Pista / detalle del objetivo actual (abajo de la lista).</summary>
        public void Objetivo(string texto) => textoObjetivo.text = texto ?? "";

        RectTransform panelObjetivos;
        Text textoListaObjetivos;
        string[] pasosObjetivo;
        int pasoActual;

        /// <summary>Define la lista de pasos del nivel y muestra el panel.</summary>
        public void DefinirObjetivos(string[] pasos)
        {
            pasosObjetivo = pasos;
            pasoActual = 0;
            DibujarObjetivos();
        }

        /// <summary>Marca como hechos todos los pasos anteriores a 'indice' y resalta ese.</summary>
        public void AvanzarObjetivo(int indice)
        {
            if (pasosObjetivo == null || indice <= pasoActual) return;
            pasoActual = Mathf.Min(indice, pasosObjetivo.Length);
            DibujarObjetivos();
            if (pasoActual < pasosObjetivo.Length) Aviso("Nuevo objetivo: " + pasosObjetivo[pasoActual], 3f);
        }

        bool objetivosHabilitados, objetivosOcultosPorJugador;

        /// <summary>Habilita/deshabilita el panel (el jugador además lo puede ocultar con Tab).</summary>
        public void MostrarObjetivos(bool mostrar)
        {
            objetivosHabilitados = mostrar;
            ActualizarVisibilidadObjetivos();
        }

        void ActualizarVisibilidadObjetivos()
        {
            if (panelObjetivos != null)
                panelObjetivos.gameObject.SetActive(objetivosHabilitados && !objetivosOcultosPorJugador && pasosObjetivo != null);
        }

        Text textoZoom;

        /// <summary>Muestra "Zoom 150%" debajo de la mira mientras se mantiene Z (0 = ocultar).</summary>
        public void IndicadorZoom(float zoom)
        {
            if (textoZoom == null)
            {
                textoZoom = CrearTexto(raiz, "Zoom", 20, TextAnchor.UpperCenter);
                Anclar(textoZoom.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(500, 30), new Vector2(0.5f, 1));
                textoZoom.color = new Color(1f, 1f, 1f, 0.8f);
            }
            textoZoom.text = zoom > 1.01f ? $"Zoom {Mathf.RoundToInt(zoom * 100f)}%  ·  rueda del mouse" : "";
        }

        void DibujarObjetivos()
        {
            if (pasosObjetivo == null) return;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pasosObjetivo.Length; i++)
            {
                if (i < pasoActual) sb.Append("<color=#7f8a7f>[x]  ").Append(pasosObjetivo[i]).Append("</color>");
                else if (i == pasoActual) sb.Append("<color=#ffd866><b>>   ").Append(pasosObjetivo[i]).Append("</b></color>");
                else sb.Append("<color=#a8a8a8>[  ]  ").Append(pasosObjetivo[i]).Append("</color>");
                if (i < pasosObjetivo.Length - 1) sb.Append('\n');
            }
            textoListaObjetivos.text = sb.ToString();
        }

        public void Subtitulo(string quien, string texto, float duracion)
        {
            if (rutSub != null) StopCoroutine(rutSub);
            textoSubtitulo.text = string.IsNullOrEmpty(quien) ? texto : $"<b>{quien}:</b> {texto}";
            rutSub = StartCoroutine(OcultarLuego(textoSubtitulo, duracion));
        }

        public void Aviso(string texto, float duracion)
        {
            if (rutAviso != null) StopCoroutine(rutAviso);
            textoAviso.text = texto;
            rutAviso = StartCoroutine(OcultarLuego(textoAviso, duracion));
        }

        public void MensajeCentral(string titulo, string subtitulo)
        {
            textoCentral.text = titulo ?? "";
            textoCentralSub.text = subtitulo ?? "";
        }

        public void Fundir(float alfaObjetivo, float duracion)
        {
            if (rutFundido != null) StopCoroutine(rutFundido);
            rutFundido = StartCoroutine(RutinaFundido(alfaObjetivo, duracion));
        }

        public void FundidoInstantaneo(float alfa) { if (rutFundido != null) StopCoroutine(rutFundido); SetAlfa(fundido, alfa); }

        public void Destello(Color color, float intensidad, float duracion)
        {
            if (rutDestello != null) StopCoroutine(rutDestello);
            rutDestello = StartCoroutine(RutinaDestello(color, intensidad, duracion));
        }

        bool hudJuegoVisible = true;

        /// <summary>Oculta/muestra cordura, ruido y flash (para cinemáticas).</summary>
        public void MostrarHUDJuego(bool mostrar)
        {
            hudJuegoVisible = mostrar;
            barraCordura.transform.parent.parent.gameObject.SetActive(mostrar);
            if (!mostrar) grupoFlash.SetActive(false);
        }

        public void MostrarPausa(bool mostrar) => panelPausa.SetActive(mostrar);

        // ------------------------------------------------------------------ Actualización

        void Update()
        {
            if (!GestorJuego.EstaPausado && ControlesJuego.AlternarObjetivos != null
                && ControlesJuego.AlternarObjetivos.WasPressedThisFrame() && objetivosHabilitados)
            {
                objetivosOcultosPorJugador = !objetivosOcultosPorJugador;
                ActualizarVisibilidadObjetivos();
            }

            var c = Cordura.Instancia;
            if (c != null)
            {
                Llenar(barraCordura, c.Normalizada);
                bool baja = c.Normalizada < c.umbralBlur;
                barraCordura.color = baja ? Color.Lerp(ColorCorduraBaja, Color.white, Mathf.PingPong(Time.time * 2f, 0.4f)) : ColorCordura;
                textoCordura.text = $"CORDURA  {Mathf.CeilToInt(c.Valor)}";
            }

            var r = SistemaRuido.Instancia;
            if (r != null)
            {
                float max = Mathf.Max(1f, r.RadioMaximoPosible);
                Llenar(barraRuido, Mathf.Clamp01(r.Radio / max));
                Llenar(barraRuidoMic, Mathf.Clamp01(r.RadioMicrofono / max));
                textoRuido.text = r.MicrofonoActivo ? "RUIDO  (micrófono activo)" : "RUIDO  (sin micrófono)";
            }

            var f = FlashCelular.Instancia;
            if (f != null && hudJuegoVisible)
            {
                grupoFlash.SetActive(f.TieneAlguno);
                Llenar(barraFlash, f.Recarga01);
                barraFlash.color = f.Disponible ? new Color(1f, 0.95f, 0.6f) : new Color(0.5f, 0.5f, 0.5f);
                textoFlash.text = $"FLASH {f.Actual.nombre.ToUpper()}  [F]" + (f.Disponible ? "  LISTO" : "");
            }
        }

        // ------------------------------------------------------------------ Construcción

        void Construir()
        {
            var canvasGo = new GameObject("CanvasHUD", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            raiz = canvasGo.GetComponent<RectTransform>();

            // Mira
            var mira = CrearImagen(raiz, "Mira", new Color(1f, 1f, 1f, 0.6f));
            Anclar(mira.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));

            // Panel inferior izquierdo: cordura + ruido
            var panel = new GameObject("Estado", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(raiz, false);
            Anclar(panel, Vector2.zero, Vector2.zero, new Vector2(40, 40), new Vector2(420, 120));
            panel.pivot = Vector2.zero;
            textoCordura = CrearTexto(panel, "TxtCordura", 22, TextAnchor.LowerLeft);
            Anclar(textoCordura.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -14), new Vector2(420, 30), new Vector2(0, 1));
            barraCordura = CrearBarra(panel, new Vector2(0, -50), new Vector2(380, 16), ColorCordura, out _);
            textoRuido = CrearTexto(panel, "TxtRuido", 18, TextAnchor.LowerLeft);
            Anclar(textoRuido.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -72), new Vector2(420, 26), new Vector2(0, 1));
            barraRuido = CrearBarra(panel, new Vector2(0, -102), new Vector2(380, 10), new Color(0.85f, 0.85f, 0.85f), out var fondoRuido);
            barraRuidoMic = CrearImagen(fondoRuido, "Mic", new Color(0.95f, 0.45f, 0.3f, 0.9f));
            Anclar(barraRuidoMic.rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero, Vector2.zero);

            // Flash (abajo a la derecha)
            var flash = new GameObject("Flash", typeof(RectTransform)).GetComponent<RectTransform>();
            flash.SetParent(raiz, false);
            Anclar(flash, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(380, 60), new Vector2(1, 0));
            grupoFlash = flash.gameObject;
            textoFlash = CrearTexto(flash, "TxtFlash", 20, TextAnchor.LowerRight);
            Anclar(textoFlash.rectTransform, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 30), new Vector2(0.5f, 1));
            barraFlash = CrearBarra(flash, new Vector2(0, -40), new Vector2(380, 12), Color.white, out _);
            grupoFlash.SetActive(false);

            // Panel de objetivos (a la izquierda): lista de pasos + pista del paso actual
            var fondoObj = CrearImagen(raiz, "PanelObjetivos", new Color(0f, 0f, 0f, 0.45f));
            panelObjetivos = fondoObj.rectTransform;
            Anclar(panelObjetivos, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 90), new Vector2(560, 330), new Vector2(0, 0.5f));
            var tituloObj = CrearTexto(panelObjetivos, "Titulo", 22, TextAnchor.UpperLeft);
            Anclar(tituloObj.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -12), new Vector2(-36, 30), new Vector2(0, 1));
            tituloObj.text = "<b>OBJETIVOS</b>  <size=15><color=#bbbbbb>(Tab para ocultar)</color></size>";
            tituloObj.color = new Color(1f, 0.85f, 0.4f);
            textoListaObjetivos = CrearTexto(panelObjetivos, "Lista", 20, TextAnchor.UpperLeft);
            Anclar(textoListaObjetivos.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -48), new Vector2(-36, 190), new Vector2(0, 1));
            textoListaObjetivos.lineSpacing = 1.15f;
            textoObjetivo = CrearTexto(panelObjetivos, "Pista", 17, TextAnchor.UpperLeft);
            Anclar(textoObjetivo.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 12), new Vector2(-36, 80), new Vector2(0, 0));
            textoObjetivo.color = new Color(0.85f, 0.85f, 0.85f);
            panelObjetivos.gameObject.SetActive(false);

            // Aviso (arriba al centro)
            textoAviso = CrearTexto(raiz, "Aviso", 26, TextAnchor.UpperCenter);
            Anclar(textoAviso.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1200, 60), new Vector2(0.5f, 1));
            textoAviso.color = new Color(1f, 0.92f, 0.75f);

            // Cartel de interacción
            var prompt = new GameObject("Prompt", typeof(RectTransform)).GetComponent<RectTransform>();
            prompt.SetParent(raiz, false);
            Anclar(prompt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(900, 80));
            grupoPrompt = prompt.gameObject;
            textoPrompt = CrearTexto(prompt, "TxtPrompt", 24, TextAnchor.MiddleCenter);
            Anclar(textoPrompt.rectTransform, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 40), new Vector2(0.5f, 1));
            barraPrompt = CrearBarra(prompt, new Vector2(260, -50), new Vector2(380, 8), new Color(1f, 0.95f, 0.6f), out _);
            grupoPrompt.SetActive(false);

            // Subtítulos
            textoSubtitulo = CrearTexto(raiz, "Subtitulo", 30, TextAnchor.LowerCenter);
            Anclar(textoSubtitulo.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(1500, 120), new Vector2(0.5f, 0));

            // Destello y fundido (pantalla completa)
            destello = CrearImagen(raiz, "Destello", new Color(1, 1, 1, 0));
            Estirar(destello.rectTransform);
            destello.raycastTarget = false;
            fundido = CrearImagen(raiz, "Fundido", new Color(0, 0, 0, 1));
            Estirar(fundido.rectTransform);
            fundido.raycastTarget = false;

            // Mensaje central (encima del fundido)
            textoCentral = CrearTexto(raiz, "Central", 64, TextAnchor.MiddleCenter);
            Anclar(textoCentral.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1600, 100));
            textoCentralSub = CrearTexto(raiz, "CentralSub", 28, TextAnchor.MiddleCenter);
            Anclar(textoCentralSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1600, 80));
            textoCentralSub.color = new Color(0.8f, 0.8f, 0.8f);

            // Pausa
            var pausa = CrearImagen(raiz, "Pausa", new Color(0, 0, 0, 0.75f));
            Estirar(pausa.rectTransform);
            panelPausa = pausa.gameObject;
            textoPausa = CrearTexto(pausa.rectTransform, "TxtPausa", 34, TextAnchor.MiddleCenter);
            Estirar(textoPausa.rectTransform);
            textoPausa.text = "PAUSA\n\n<size=24>Esc — Seguir      R — Reiniciar el nivel\n\nWASD moverse · Shift correr · C agacharse · E interactuar · F flash\nZ zoom (rueda: hasta 200%) · Tab mostrar/ocultar objetivos</size>";
            panelPausa.SetActive(false);

            textoSubtitulo.text = textoAviso.text = textoObjetivo.text = textoCentral.text = textoCentralSub.text = "";
        }

        Text CrearTexto(RectTransform padre, string nombre, int tam, TextAnchor ancla)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = fuente;
            t.fontSize = tam;
            t.alignment = ancla;
            t.color = Color.white;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var s = go.GetComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.9f);
            s.effectDistance = new Vector2(2, -2);
            return t;
        }

        Image CrearImagen(RectTransform padre, string nombre, Color color)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Image CrearBarra(RectTransform padre, Vector2 pos, Vector2 tam, Color color, out RectTransform fondo)
        {
            var bg = CrearImagen(padre, "Fondo", new Color(0, 0, 0, 0.55f));
            Anclar(bg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), pos, tam, new Vector2(0, 1));
            fondo = bg.rectTransform;
            var relleno = CrearImagen(bg.rectTransform, "Relleno", color);
            Anclar(relleno.rectTransform, Vector2.zero, new Vector2(1, 1), Vector2.zero, Vector2.zero, Vector2.zero);
            return relleno;
        }

        static void Llenar(Image barra, float valor)
        {
            var rt = barra.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(valor), 1f);
            rt.offsetMax = Vector2.zero;
        }

        static void Anclar(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 tam, Vector2? pivot = null)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = tam;
        }

        static void Estirar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void SetAlfa(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

        IEnumerator OcultarLuego(Text t, float seg)
        {
            yield return new WaitForSeconds(seg);
            t.text = "";
        }

        IEnumerator RutinaFundido(float objetivo, float duracion)
        {
            float desde = fundido.color.a, t = 0f;
            while (t < duracion)
            {
                t += Time.unscaledDeltaTime;
                SetAlfa(fundido, Mathf.Lerp(desde, objetivo, t / duracion));
                yield return null;
            }
            SetAlfa(fundido, objetivo);
        }

        IEnumerator RutinaDestello(Color color, float intensidad, float duracion)
        {
            float t = 0f;
            while (t < duracion)
            {
                t += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(intensidad, 0f, t / duracion);
                destello.color = color;
                yield return null;
            }
            color.a = 0f;
            destello.color = color;
        }
    }
}
