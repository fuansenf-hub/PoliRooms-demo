using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Collections;

public class MonstruoPersigue : MonoBehaviour
{
    public enum EstadoMonstruo { Patrullando, Persiguiendo }
    public EstadoMonstruo estadoActual = EstadoMonstruo.Patrullando;

    private NavMeshAgent agente;
    private Transform jugador;
    private MovimientoJugador scriptJugador;
    private Transform raizVisual;
        private float alturaLocalVisualInicial;
    private Collider[] collidersMonstruo;
    private Collider[] collidersJugador;
    private float radioHorizontalMonstruo;
    private float radioHorizontalJugador;
    
    [Header("Componentes de Audio y Animación")]
    private Animator animator; 
    private AudioSource audioSusto;
    public AudioSource audioPasos; // NUEVO: Arrastra aquí el AudioSource de los pasos
    public AudioSource audioGaspJugador;
    private bool gaspDeteccionReproducido;
    
    public AudioClip clipSusto; 
    public float alturaCabeza = 1.3f; 
    public float distanciaParaAtrapar = 0.5f; 

    [Header("Animación de captura")]
    [Min(0f)] public float avanceCaptura = 0.25f;
    [Min(0.05f)] public float duracionAnimacionCaptura = 0.65f;
    [Min(0f)] public float esperaAntesDeReiniciar = 1.5f;

    [Header("Sentidos e Inteligencia")]
    public float distanciaVision = 10f; 
    public float anguloVision = 75f;    
    public float tiempoParaRendirse = 4f; 
    private float temporizadorOlvido = 0f;

    [Header("Patrullaje")]
    public float radioPatrullaje = 15f; 
    public float velocidadPatrulla = 2.5f; 
    public float velocidadPersecucion = 6f; 
    
    public float tiempoEsperaPatrulla = 3f; 
    private float tiempoEsperando = 0f;

    private bool atrapado = false;
    private const float InclinacionExtraCamaraCaptura = 35f;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        collidersMonstruo = GetComponentsInChildren<Collider>(true);
        radioHorizontalMonstruo = RadioHorizontalCollider(collidersMonstruo);
        if (agente != null)
            agente.baseOffset = 0f;

        animator = GetComponentInChildren<Animator>();        
        if (animator != null)
        {
            animator.applyRootMotion = false;
            raizVisual = animator.transform;
            alturaLocalVisualInicial = raizVisual.localPosition.y;
        }

        // Asignamos el AudioSource principal (Susto) que ya tenías
        audioSusto = GetComponent<AudioSource>();
        
        GameObject objetoJugador = GameObject.FindGameObjectWithTag("Player");
        if (objetoJugador != null)
        {
            jugador = objetoJugador.transform;
            scriptJugador = objetoJugador.GetComponent<MovimientoJugador>();
            collidersJugador = objetoJugador.GetComponentsInChildren<Collider>(true);
            radioHorizontalJugador = RadioHorizontalCollider(collidersJugador);
            if (audioGaspJugador == null)
                audioGaspJugador = objetoJugador.GetComponent<AudioSource>();
        }

        BuscarPuntoPatrullaje();
    }

    void Update()
    {
        if (jugador != null && !atrapado)
        {
            float distancia = DistanciaHorizontalAlJugador();

            if (distancia <= DistanciaActivacionCaptura())
            {
                StartCoroutine(AtraparJugadorRutina());
                return; 
            }

            bool teVeOEscucha = DetectarJugador();
            bool debeMoverse = false; // Variable maestra para controlar animaciones y sonido
            bool inicioPersecucion = false;

            if (teVeOEscucha)
            {
                if (!gaspDeteccionReproducido && audioGaspJugador != null)
                {
                    audioGaspJugador.Play();
                    gaspDeteccionReproducido = true;
                }

                inicioPersecucion = estadoActual != EstadoMonstruo.Persiguiendo;
                estadoActual = EstadoMonstruo.Persiguiendo;
                temporizadorOlvido = 0f; 
                tiempoEsperando = 0f; 
                agente.isStopped = false; 
                agente.speed = velocidadPersecucion;
                agente.stoppingDistance = distanciaParaAtrapar;
                agente.SetDestination(jugador.position); 
                
                debeMoverse = true; // Si te persigue, siempre se mueve
            }
            else
            {
                if (estadoActual == EstadoMonstruo.Persiguiendo)
                {
                    temporizadorOlvido += Time.deltaTime; 
                    debeMoverse = true; // Sigue moviéndose hacia tu última posición
                    
                    if (temporizadorOlvido >= tiempoParaRendirse)
                    {
                        estadoActual = EstadoMonstruo.Patrullando;
                        agente.speed = velocidadPatrulla;
                        agente.stoppingDistance = 0f;
                        BuscarPuntoPatrullaje();
                    }
                }
                else if (estadoActual == EstadoMonstruo.Patrullando)
                {
                    if (!agente.pathPending && agente.remainingDistance <= agente.stoppingDistance)
                    {
                        agente.isStopped = true; 
                        tiempoEsperando += Time.deltaTime;
                        debeMoverse = false; // ESTÁ ESPERANDO. FORZAMOS EL DETENIMIENTO.
                        
                        if (tiempoEsperando >= tiempoEsperaPatrulla)
                        {
                            BuscarPuntoPatrullaje();
                            agente.isStopped = false; 
                            tiempoEsperando = 0f; 
                            debeMoverse = true;
                        }
                    }
                    else
                    {
                        debeMoverse = true; // Si no ha llegado a su destino, sigue moviéndose
                    }
                }
            }

            // --- Control Absoluto de Animación y Sonido ---
            if (animator != null)
            {
                animator.SetBool("Caminando", debeMoverse);
                animator.SetBool("Persiguiendo", estadoActual == EstadoMonstruo.Persiguiendo);
                animator.speed = 1f;
                if (inicioPersecucion)
                    animator.SetTrigger("InicioPersecucion");
            }

            if (audioPasos != null)
            {
                if (debeMoverse)
                {
                    if (!audioPasos.isPlaying) audioPasos.Play(); // Enciende los pasos
                    // Ajusta el tono del sonido (pitch) para que suene más rápido al correr
                    audioPasos.pitch = (estadoActual == EstadoMonstruo.Persiguiendo) ? 1.3f : 1.0f;
                }
                else
                {
                    if (audioPasos.isPlaying) audioPasos.Pause(); // Congela el sonido exacto donde quedó
                }
            }
        }
    }
    bool DetectarJugador()
    {
        float distancia = Vector3.Distance(transform.position, jugador.position);
        if (scriptJugador != null && distancia <= scriptJugador.nivelRuido) return true;

        if (distancia > distanciaVision)
            return false;

        Vector3 direccionAlJugador = (jugador.position - transform.position).normalized;
        Vector3 frenteMonstruo = raizVisual != null ? raizVisual.forward : transform.forward;
        float angulo = Vector3.Angle(frenteMonstruo, direccionAlJugador);

        if (angulo < anguloVision)
        {
            Vector3 origenRayo = transform.position + Vector3.up * 1f; 
            Vector3 destinoRayo = jugador.position + Vector3.up * 1f;
            Vector3 direccionRayo = destinoRayo - origenRayo;
            RaycastHit hit;

            if (Physics.Raycast(origenRayo, direccionRayo.normalized, out hit, direccionRayo.magnitude))
            {
                Transform alcanzado = hit.collider.transform;
                if (alcanzado == jugador || alcanzado.IsChildOf(jugador))
                    return true;
            }
        }
        return false; 
    }

    float DistanciaActivacionCaptura()
    {
        return Mathf.Max(distanciaParaAtrapar, radioHorizontalMonstruo + radioHorizontalJugador);
    }

    float RadioHorizontalCollider(Collider[] colliders)
    {
        float radio = 0f;
        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled)
                continue;

            Bounds bounds = col.bounds;
            radio = Mathf.Max(radio, bounds.extents.x, bounds.extents.z);
        }
        return radio;
    }

    float DistanciaHorizontalAlJugador()
    {
        Vector3 diferencia = jugador.position - transform.position;
        diferencia.y = 0f;
        return diferencia.magnitude;
    }

    void BuscarPuntoPatrullaje()
    {
        Vector3 direccionAleatoria = Random.insideUnitSphere * radioPatrullaje;
        direccionAleatoria += transform.position;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(direccionAleatoria, out hit, radioPatrullaje, 1))
        {
            agente.SetDestination(hit.position);
        }
    }

    IEnumerator AtraparJugadorRutina()
    {
        atrapado = true;
        DesactivarColisionesCaptura();
        if (agente != null && agente.isOnNavMesh)
        {
            agente.isStopped = true;
            agente.updateRotation = false;
            agente.ResetPath();
            agente.velocity = Vector3.zero;
        }
        
        if (animator != null)
        {
            animator.SetBool("Caminando", false);
            animator.SetBool("Persiguiendo", false);
            animator.ResetTrigger("InicioPersecucion");
            animator.SetTrigger("Capturar");
        }
        if (audioPasos != null) audioPasos.Stop();
        if (audioSusto != null && clipSusto != null) audioSusto.PlayOneShot(clipSusto);

        if (scriptJugador != null) scriptJugador.enabled = false;

        Camera camaraJugador = jugador.GetComponentInChildren<Camera>(true);
        Transform transformCamara = camaraJugador != null ? camaraJugador.transform : null;
        Vector3 puntoMirada = transformCamara != null ? transformCamara.position : jugador.position;
        Vector3 direccionAlJugador = puntoMirada - PosicionCabezaMonstruo();
        direccionAlJugador.y = 0f;
        Quaternion rotacionMonstruoInicial = transform.rotation;
        Quaternion rotacionMonstruoObjetivo = rotacionMonstruoInicial;
        if (direccionAlJugador.sqrMagnitude > 0.001f)
        {
            // Usa el frente real del modelo visual; el FBX puede tener un giro local
            // distinto al objeto que lleva el NavMeshAgent.
            Vector3 frenteVisual = raizVisual != null ? raizVisual.forward : transform.forward;
            frenteVisual.y = 0f;
            if (frenteVisual.sqrMagnitude > 0.001f)
            {
                float diferenciaYaw = Vector3.SignedAngle(frenteVisual.normalized,
                    direccionAlJugador.normalized, Vector3.up);
                rotacionMonstruoObjetivo = Quaternion.AngleAxis(diferenciaYaw, Vector3.up)
                    * rotacionMonstruoInicial;
            }
        }

        Quaternion rotacionJugadorInicial = jugador.rotation;
        Quaternion rotacionCamaraInicial = transformCamara != null ? transformCamara.rotation : Quaternion.identity;

        float tiempo = 0f;
        while (tiempo < duracionAnimacionCaptura)
        {
            tiempo += Time.deltaTime;
            float progreso = Mathf.Clamp01(tiempo / duracionAnimacionCaptura);
            float suave = Mathf.SmoothStep(0f, 1f, progreso);
            transform.rotation = Quaternion.Slerp(rotacionMonstruoInicial, rotacionMonstruoObjetivo, suave);

            if (avanceCaptura > 0f)
            {
                Vector3 desplazamiento = (rotacionMonstruoObjetivo * Vector3.forward)
                    * (avanceCaptura * (Mathf.SmoothStep(0f, 1f, progreso) - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progreso - Time.deltaTime / duracionAnimacionCaptura))));
                if (agente != null && agente.isOnNavMesh)
                    agente.Move(desplazamiento);
                else
                    transform.position += desplazamiento;
            }

            if (transformCamara != null)
            {
                Vector3 objetivoCabeza = PosicionCabezaMonstruo();
                Vector3 direccionJugador = objetivoCabeza - jugador.position;
                direccionJugador.y = 0f;
                if (direccionJugador.sqrMagnitude > 0.001f)
                {
                    Quaternion rotacionJugadorObjetivo = Quaternion.LookRotation(direccionJugador.normalized, Vector3.up);
                    jugador.rotation = Quaternion.Slerp(rotacionJugadorInicial, rotacionJugadorObjetivo, suave);
                }

                Vector3 direccionCabeza = objetivoCabeza - transformCamara.position;
                if (direccionCabeza.sqrMagnitude > 0.001f)
                {
                    Quaternion rotacionCamaraObjetivo = RotacionCamaraHaciaCabeza(direccionCabeza);
                    transformCamara.rotation = Quaternion.Slerp(rotacionCamaraInicial, rotacionCamaraObjetivo, suave);
                }
            }

            yield return null;
        }

        transform.rotation = rotacionMonstruoObjetivo;
        Vector3 direccionJugadorFinal = PosicionCabezaMonstruo() - jugador.position;
        direccionJugadorFinal.y = 0f;
        if (direccionJugadorFinal.sqrMagnitude > 0.001f)
            jugador.rotation = Quaternion.LookRotation(direccionJugadorFinal.normalized, Vector3.up);
        if (transformCamara != null)
            transformCamara.rotation = RotacionCamaraHaciaCabeza(
                PosicionCabezaMonstruo() - transformCamara.position);

        // Reinicia un segundo antes de que termine el clip de captura.
        if (animator != null)
        {
            while (animator.IsInTransition(0)
                && animator.GetNextAnimatorStateInfo(0).IsName("Captura"))
                yield return null;

            AnimatorStateInfo estadoCaptura = animator.GetCurrentAnimatorStateInfo(0);
            if (estadoCaptura.IsName("Captura"))
            {
                AnimatorClipInfo[] clipsCaptura = animator.GetCurrentAnimatorClipInfo(0);
                float duracionClip = clipsCaptura.Length > 0 ? clipsCaptura[0].clip.length : 0f;
                float tiempoAntesDeFin = duracionClip > 0f
                    ? Mathf.Clamp01(1f - (1f + Mathf.Max(0f, esperaAntesDeReiniciar)) / duracionClip)
                    : 1f;

                while (animator.GetCurrentAnimatorStateInfo(0).IsName("Captura")
                    && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < tiempoAntesDeFin)
                    yield return null;
            }
        }

        yield return new WaitForSeconds(esperaAntesDeReiniciar);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void DesactivarColisionesCaptura()
    {
        foreach (Collider col in collidersMonstruo)
        {
            if (col != null)
                col.enabled = false;
        }

        if (jugador == null)
            return;

        foreach (Collider col in collidersJugador)
        {
            if (col != null)
                col.enabled = false;
        }
    }

    private Vector3 PosicionCabezaMonstruo()
    {
        return transform.position + Vector3.up * alturaCabeza;
    }

    private Quaternion RotacionCamaraHaciaCabeza(Vector3 direccionCabeza)
    {
        if (direccionCabeza.sqrMagnitude < 0.001f)
            return transform.rotation;

        Vector3 direccionElevada = direccionCabeza.normalized
            + Vector3.up * Mathf.Tan(InclinacionExtraCamaraCaptura * Mathf.Deg2Rad);
        return Quaternion.LookRotation(direccionElevada, Vector3.up);
    }

}











