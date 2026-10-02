using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement; // Necesario para reiniciar el nivel

public class MonstruoPersigue : MonoBehaviour
{
    private NavMeshAgent agente;
    private Transform jugador;
    public float distanciaParaAtrapar = 1.5f;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        
        GameObject objetoJugador = GameObject.FindGameObjectWithTag("Player");
        if (objetoJugador != null)
        {
            jugador = objetoJugador.transform;
        }
    }

    void Update()
    {
        if (jugador != null)
        {
            // 1. Perseguir al jugador
            agente.SetDestination(jugador.position);

            // 2. Comprobar si lo alcanzó
            float distancia = Vector3.Distance(transform.position, jugador.position);
            
            if (distancia <= distanciaParaAtrapar)
            {
                AtraparJugador();
            }
        }
    }

    void AtraparJugador()
    {
        // Aquí reiniciamos la escena actual para crear el bucle
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}