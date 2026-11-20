using UnityEngine.InputSystem;

namespace PoliRooms
{
    /// <summary>
    /// Todas las acciones de input del juego (Input System nuevo), definidas por código.
    /// Teclado + mouse y joystick. Se crean una sola vez y quedan habilitadas.
    /// </summary>
    public static class ControlesJuego
    {
        static InputActionMap mapa;

        public static InputAction Mover { get; private set; }
        public static InputAction Mirar { get; private set; }
        public static InputAction MirarStick { get; private set; }
        public static InputAction Correr { get; private set; }
        public static InputAction Agacharse { get; private set; }
        public static InputAction Interactuar { get; private set; }
        public static InputAction Flash { get; private set; }
        public static InputAction Pausa { get; private set; }
        public static InputAction Reiniciar { get; private set; }
        public static InputAction Saltear { get; private set; }
        public static InputAction Zoom { get; private set; }
        public static InputAction RuedaZoom { get; private set; }
        public static InputAction AlternarObjetivos { get; private set; }
        /// <summary>Truco de prueba: Ctrl + M hace desaparecer / aparecer a Mayra.</summary>
        public static InputAction TrucoOcultarMayra { get; private set; }

        public static void Asegurar()
        {
            if (mapa != null) return;

            mapa = new InputActionMap("Jugador");

            Mover = mapa.AddAction("Mover", InputActionType.Value);
            Mover.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Mover.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            Mover.AddBinding("<Gamepad>/leftStick");

            Mirar = mapa.AddAction("Mirar", InputActionType.PassThrough, "<Mouse>/delta");
            MirarStick = mapa.AddAction("MirarStick", InputActionType.Value, "<Gamepad>/rightStick");

            Correr = mapa.AddAction("Correr", InputActionType.Button, "<Keyboard>/leftShift");
            Correr.AddBinding("<Gamepad>/leftStickPress");

            Agacharse = mapa.AddAction("Agacharse", InputActionType.Button, "<Keyboard>/c");
            Agacharse.AddBinding("<Keyboard>/leftCtrl");
            Agacharse.AddBinding("<Gamepad>/buttonEast");

            Interactuar = mapa.AddAction("Interactuar", InputActionType.Button, "<Keyboard>/e");
            Interactuar.AddBinding("<Gamepad>/buttonSouth");

            Flash = mapa.AddAction("Flash", InputActionType.Button, "<Keyboard>/f");
            Flash.AddBinding("<Mouse>/rightButton");
            Flash.AddBinding("<Gamepad>/rightTrigger");

            Pausa = mapa.AddAction("Pausa", InputActionType.Button, "<Keyboard>/escape");
            Pausa.AddBinding("<Gamepad>/start");

            Reiniciar = mapa.AddAction("Reiniciar", InputActionType.Button, "<Keyboard>/r");
            Reiniciar.AddBinding("<Gamepad>/select");

            Saltear = mapa.AddAction("Saltear", InputActionType.Button, "<Keyboard>/space");
            Saltear.AddBinding("<Keyboard>/enter");
            Saltear.AddBinding("<Gamepad>/buttonNorth");

            Zoom = mapa.AddAction("Zoom", InputActionType.Button, "<Keyboard>/z");
            Zoom.AddBinding("<Gamepad>/leftTrigger");
            RuedaZoom = mapa.AddAction("RuedaZoom", InputActionType.PassThrough, "<Mouse>/scroll/y");
            RuedaZoom.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/dpad/down")
                .With("Positive", "<Gamepad>/dpad/up");

            AlternarObjetivos = mapa.AddAction("AlternarObjetivos", InputActionType.Button, "<Keyboard>/tab");
            AlternarObjetivos.AddBinding("<Gamepad>/dpad/left");

            TrucoOcultarMayra = mapa.AddAction("TrucoOcultarMayra", InputActionType.Button);
            TrucoOcultarMayra.AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", "<Keyboard>/ctrl")
                .With("Button", "<Keyboard>/m");

            mapa.Enable();
        }
    }
}
