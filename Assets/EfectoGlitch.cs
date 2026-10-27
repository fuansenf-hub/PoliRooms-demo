using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class EfectoGlitch : MonoBehaviour
{
    [Header("Referencias")]
    public Transform jugador;
    public Transform monstruo;
    
    // NUEVO: Referencia al Audio Source de la estática en la cámara
    private AudioSource audioEstatica;

    [Header("Ajustes de Terror")]
    public float distanciaParaEmpezar = 15f; 
    
    [Header("Intensidad Máxima (Ajustable)")]
    public float distorsionMaxima = -100f; 
    public float aberracionMaxima = 1f;    
    public float granoMaximo = 1f;         
    public float volumenEstaticaMaximo = 1f; // Volumen máximo del ruido blanco

    private PostProcessVolume volumen;
    private LensDistortion distorsion;
    private ChromaticAberration aberracion;
    private Grain grano; 

    void Start()
    {
        volumen = GetComponent<PostProcessVolume>();
        
        volumen.profile.TryGetSettings(out distorsion);
        volumen.profile.TryGetSettings(out aberracion);
        volumen.profile.TryGetSettings(out grano);

        // Buscamos el AudioSource de la estática en la cámara del jugador
        if (jugador != null)
        {
            audioEstatica = jugador.GetComponentInChildren<AudioSource>();
            if (audioEstatica != null && !audioEstatica.isPlaying)
            {
                audioEstatica.Play(); // Nos aseguramos de que empiece a sonar
            }
        }
    }

    void Update()
    {
        if (jugador == null || monstruo == null) return;

        float distancia = Vector3.Distance(jugador.position, monstruo.position);

        if (distancia < distanciaParaEmpezar)
        {
            float porcentajeBase = 1f - (distancia / distanciaParaEmpezar);
            float intensidadAgresiva = porcentajeBase * porcentajeBase; 

            // Efectos visuales
            if (distorsion != null) distorsion.intensity.value = intensidadAgresiva * distorsionMaxima; 
            if (aberracion != null) aberracion.intensity.value = intensidadAgresiva * aberracionMaxima; 
            if (grano != null) grano.intensity.value = 0.3f + (intensidadAgresiva * (granoMaximo - 0.3f)); 

            // NUEVO: El volumen de la estática acompaña exactamente la intensidad del glitch
            if (audioEstatica != null)
            {
                audioEstatica.volume = intensidadAgresiva * volumenEstaticaMaximo;
            }
        }
        else
        {
            // Estado normal y tranquilo (Silencio de estática y efectos apagados)
            if (distorsion != null) distorsion.intensity.value = 0f;
            if (aberracion != null) aberracion.intensity.value = 0f;
            if (grano != null) grano.intensity.value = 0.3f; 

            if (audioEstatica != null)
            {
                audioEstatica.volume = 0f; // Apaga la estática cuando el monstruo está lejos
            }
        }
    }
}