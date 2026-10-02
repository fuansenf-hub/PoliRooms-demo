using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MovimientoJugador : MonoBehaviour
{
    // Ajustes de velocidad
    public float velocidad = 5.0f;
    public float velocidadSprint = 8.0f; // Velocidad al correr
    
    // Ajustes de cámara
    public float sensibilidadRatón = 2.0f;
    private float rotaciónX = 0f;
    
    // Referencias
    private Transform camaraJugador; 
    private CharacterController controller; 

    void Start()
    {
        // Ocultar y bloquear el ratón en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked; 
        
        // Buscar los componentes necesarios
        camaraJugador = GetComponentInChildren<Camera>().transform;
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // --- 1. ROTACIÓN (MOUSE) ---
        float mouseX = Input.GetAxisRaw("Mouse X") * sensibilidadRatón;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensibilidadRatón;

        // Girar la cámara arriba/abajo
        if (camaraJugador != null)
        {
            rotaciónX -= mouseY;
            rotaciónX = Mathf.Clamp(rotaciónX, -90f, 90f);
            camaraJugador.localRotation = Quaternion.Euler(rotaciónX, 0f, 0f);
        }

        // Girar el cuerpo a los lados
        transform.Rotate(Vector3.up * mouseX);


        // --- 2. MOVIMIENTO (WASD) ---
        float moverAdelante = 0f;
        float moverCostado = 0f;

        if (Input.GetKey(KeyCode.W)) moverAdelante += 1f;
        if (Input.GetKey(KeyCode.S)) moverAdelante -= 1f;
        if (Input.GetKey(KeyCode.D)) moverCostado += 1f;
        if (Input.GetKey(KeyCode.A)) moverCostado -= 1f;

        // Calcular la dirección hacia la que miramos
        Vector3 movimiento = (transform.right * moverCostado) + (transform.forward * moverAdelante);
        
        
        // --- 3. SPRINT (SHIFT IZQUIERDO) ---
        float velocidadActual = velocidad; // Por defecto caminamos
        
        // Si mantenemos Shift presionado, cambiamos a la velocidad de sprint
        if (Input.GetKey(KeyCode.LeftShift))
        {
            velocidadActual = velocidadSprint;
        }

        // --- 4. APLICAR MOVIMIENTO FÍSICO ---
        controller.Move(movimiento.normalized * velocidadActual * Time.deltaTime);
    }
}