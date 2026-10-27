using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MovimientoJugador : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadCaminar = 5.0f;
    public float velocidadSprint = 8.0f;
    public float velocidadAgachado = 2.5f;

    [Header("Estamina")]
    public float estaminaMaxima = 4f; 
    private float estaminaActual;
    public float tasaRecuperacion = 1.5f;
    private bool exhausto = false; 

    [Header("Físicas y Salto")]
    public float alturaSalto = 1.2f;
    public float gravedad = -9.81f;
    private Vector3 velocidadCaida;
    private bool enElSuelo;

    [Header("Agacharse y Cámara")]
    public float alturaNormal = 2.0f;
    public float alturaAgachado = 1.0f;
    public float alturaCamaraNormal = 1.5f;     
    public float alturaCamaraAgachada = 0.5f;   
    
    public float sensibilidadRatón = 2.0f;
    private float rotaciónX = 0f;

    // NUEVO: Nivel de ruido que emitimos para que el bicho nos escuche
    [HideInInspector] public float nivelRuido = 0f; 

    private Transform camaraJugador; 
    private CharacterController controller; 

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; 
        camaraJugador = GetComponentInChildren<Camera>().transform;
        controller = GetComponent<CharacterController>();
        estaminaActual = estaminaMaxima;
    }

    void Update()
    {
        // --- ROTACIÓN (MOUSE) ---
        float mouseX = Input.GetAxisRaw("Mouse X") * sensibilidadRatón;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensibilidadRatón;

        rotaciónX -= mouseY;
        rotaciónX = Mathf.Clamp(rotaciónX, -90f, 90f);
        camaraJugador.localRotation = Quaternion.Euler(rotaciónX, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        enElSuelo = controller.isGrounded;
        if (enElSuelo && velocidadCaida.y < 0) velocidadCaida.y = -2f; 

        // --- AGACHARSE SUAVE (LERP) ---
        bool agachado = Input.GetKey(KeyCode.C);
        
        float alturaObjetivo = agachado ? alturaAgachado : alturaNormal;
        float centroObjetivo = agachado ? alturaAgachado / 2f : alturaNormal / 2f;
        float camaraObjetivo = agachado ? alturaCamaraAgachada : alturaCamaraNormal;

        // Transición fluida (Lerp)
        controller.height = Mathf.Lerp(controller.height, alturaObjetivo, Time.deltaTime * 10f);
        controller.center = Vector3.Lerp(controller.center, new Vector3(0, centroObjetivo, 0), Time.deltaTime * 10f);
        
        float nuevaCamY = Mathf.Lerp(camaraJugador.localPosition.y, camaraObjetivo, Time.deltaTime * 10f);
        camaraJugador.localPosition = new Vector3(camaraJugador.localPosition.x, nuevaCamY, camaraJugador.localPosition.z);

        // --- ESTAMINA ---
        if (estaminaActual <= 0) exhausto = true; 
        else if (estaminaActual >= estaminaMaxima) exhausto = false; 

        bool intentandoCorrer = Input.GetKey(KeyCode.LeftShift) && !agachado && !exhausto;
        bool estaCorriendo = false;

        if (intentandoCorrer) {
            estaCorriendo = true;
            estaminaActual -= Time.deltaTime; 
        } else {
            estaminaActual += Time.deltaTime * tasaRecuperacion; 
        }
        estaminaActual = Mathf.Clamp(estaminaActual, 0, estaminaMaxima);

        // --- MOVIMIENTO ---
        float moverAdelante = 0f;
        float moverCostado = 0f;
        if (Input.GetKey(KeyCode.W)) moverAdelante += 1f;
        if (Input.GetKey(KeyCode.S)) moverAdelante -= 1f;
        if (Input.GetKey(KeyCode.D)) moverCostado += 1f;
        if (Input.GetKey(KeyCode.A)) moverCostado -= 1f;

        float velocidadActual = agachado ? velocidadAgachado : (estaCorriendo ? velocidadSprint : velocidadCaminar);
        Vector3 movimiento = (transform.right * moverCostado) + (transform.forward * moverAdelante);
        controller.Move(movimiento.normalized * velocidadActual * Time.deltaTime);

        // --- SISTEMA DE RUIDO ---
        if (movimiento.magnitude > 0.1f) // Si nos estamos moviendo
        {
            if (estaCorriendo) nivelRuido = 15f; // Ruido alto
            else if (agachado) nivelRuido = 3f;  // Ruido muy bajo
            else nivelRuido = 7f;                // Ruido normal al caminar
        }
        else
        {
            nivelRuido = 0f; // Si estamos quietos, somos silenciosos
        }

        // --- SALTO ---
        if (Input.GetKeyDown(KeyCode.Space) && enElSuelo && !agachado)
            velocidadCaida.y = Mathf.Sqrt(alturaSalto * -2f * gravedad);

        velocidadCaida.y += gravedad * Time.deltaTime;
        controller.Move(velocidadCaida * Time.deltaTime);
    }
}