using System.Collections.Generic;
using UnityEngine;

namespace PoliRooms
{
    /// <summary>Inventario simple de objetos clave, identificados por texto.</summary>
    public class Inventario : MonoBehaviour
    {
        public const string Nokia = "nokia";
        public const string CelularPro = "celular_pro";
        public const string LlaveGaveta = "llave_gaveta";

        public static Inventario Instancia { get; private set; }

        readonly HashSet<string> objetos = new HashSet<string>();

        public event System.Action<string> AlAgregar;
        public event System.Action<string> AlQuitar;

        void Awake() => Instancia = this;

        public bool Tiene(string id) => objetos.Contains(id);

        public void Agregar(string id)
        {
            if (objetos.Add(id)) AlAgregar?.Invoke(id);
        }

        public void Quitar(string id)
        {
            if (objetos.Remove(id)) AlQuitar?.Invoke(id);
        }
    }
}
