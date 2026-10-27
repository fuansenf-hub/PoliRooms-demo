using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class ConfiguradorAnimacionesBicho
{
    private const string CarpetaAnimaciones = "Assets/Bicho/Animaciones/";
    private const string RutaModelo = "Assets/Bicho/Modelos/ModeloNuevo/rigged-man-in-light-blue-shirt.fbx";
    private const string RutaControlador = "Assets/Bicho/Animaciones/ControladorMonstruo.controller";
    private const string RutaAnimacionCaptura = "Assets/Bicho/Animaciones/Meshy_AI_Man_in_Light_Blue_Shi_1016161154_texture@Searching Files High.fbx";

    private static readonly string[] RutasAnimacion =
    {
        CarpetaAnimaciones + "Meshy_AI_Man_in_Light_Blue_Shi_1016161154_texture@Idle.fbx",
        CarpetaAnimaciones + "Meshy_AI_Man_in_Light_Blue_Shi_1016161154_texture@Start Walking.fbx",
        CarpetaAnimaciones + "Meshy_AI_Man_in_Light_Blue_Shi_1016161154_texture@Strut Walking.fbx"
    };

    static ConfiguradorAnimacionesBicho()
    {
        EditorApplication.delayCall += Configurar;
    }

    public static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ModelImporter importerCaptura = AssetImporter.GetAtPath(RutaAnimacionCaptura) as ModelImporter;
        if (importerCaptura == null)
            return;

        ModelImporterClipAnimation[] clipsCaptura = importerCaptura.clipAnimations;
        if (clipsCaptura == null || clipsCaptura.Length == 0)
            clipsCaptura = importerCaptura.defaultClipAnimations;
        if (clipsCaptura.Length == 0)
            return;

        bool cambioCaptura = !importerCaptura.importAnimation
            || importerCaptura.animationType != ModelImporterAnimationType.Human
            || importerCaptura.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
            || clipsCaptura.Length != 1
            || clipsCaptura[0].name != "Searching Files High"
            || clipsCaptura[0].loopTime
            || !clipsCaptura[0].keepOriginalPositionY
            || !clipsCaptura[0].keepOriginalPositionXZ;

        if (cambioCaptura)
        {
            importerCaptura.importAnimation = true;
            importerCaptura.animationType = ModelImporterAnimationType.Human;
            importerCaptura.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importerCaptura.sourceAvatar = null;
            clipsCaptura = new[] { clipsCaptura[0] };
            clipsCaptura[0].name = "Searching Files High";
            clipsCaptura[0].loop = false;
            clipsCaptura[0].loopTime = false;
            clipsCaptura[0].keepOriginalPositionY = true;
            clipsCaptura[0].keepOriginalPositionXZ = true;
            importerCaptura.clipAnimations = clipsCaptura;
            importerCaptura.SaveAndReimport();
            EditorApplication.delayCall += Configurar;
            return;
        }

        bool requiereReimportacion = false;
        for (int i = 0; i < RutasAnimacion.Length; i++)
        {
            var importer = AssetImporter.GetAtPath(RutasAnimacion[i]) as ModelImporter;
            if (importer == null)
                return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            if (clips.Length == 0)
                return;

            string nombreClip = i == 0 ? "Idle" : i == 1 ? "Start Walking" : "Strut Walking";
            bool loop = i != 1;
            bool cambio = !importer.importAnimation
                || importer.animationType != ModelImporterAnimationType.Human
                || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || importer.sourceAvatar != null
                || clips.Length != 1
                || clips[0].name != nombreClip
                || clips[0].loopTime != loop
                || clips[0].keepOriginalPositionY == false
                || clips[0].keepOriginalPositionXZ == false;

            if (!cambio)
                continue;

            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            clips = new[] { clips[0] };
            clips[0].name = nombreClip;
            clips[0].loop = loop;
            clips[0].loopTime = loop;
            clips[0].keepOriginalPositionY = true;
            clips[0].keepOriginalPositionXZ = true;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            requiereReimportacion = true;
        }

        if (requiereReimportacion)
        {
            EditorApplication.delayCall += Configurar;
            return;
        }

        CrearControlador();
    }

    private static void CrearControlador()
    {
        AnimationClip idle = ObtenerClip(RutasAnimacion[0]);
        AnimationClip inicioMarcha = ObtenerClip(RutasAnimacion[1]);
        AnimationClip marcha = ObtenerClip(RutasAnimacion[2]);
        AnimationClip captura = ObtenerClip(RutaAnimacionCaptura);
        AnimatorController controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaControlador);
        if (idle == null || inicioMarcha == null || marcha == null || captura == null || controlador == null)
            return;

        if (ControladorCorrecto(controlador, idle, inicioMarcha, marcha, captura))
            return;

        List<AnimatorControllerParameter> parametros = controlador.parameters.ToList();
        AsegurarParametro(parametros, "Caminando", AnimatorControllerParameterType.Bool, false);
        AsegurarParametro(parametros, "Persiguiendo", AnimatorControllerParameterType.Bool, false);
        AsegurarParametro(parametros, "InicioPersecucion", AnimatorControllerParameterType.Trigger, false);
        AsegurarParametro(parametros, "Capturar", AnimatorControllerParameterType.Trigger, false);
        controlador.parameters = parametros.ToArray();

        AnimatorStateMachine maquina = controlador.layers[0].stateMachine;
        foreach (ChildAnimatorState estado in maquina.states)
            maquina.RemoveState(estado.state);

        AnimatorState quieto = CrearEstado(maquina, "Quieto", idle, new Vector3(0, 100, 0), 1f);
        AnimatorState inicio = CrearEstado(maquina, "InicioMarcha", inicioMarcha, new Vector3(220, 20, 0), 1f);
        AnimatorState merodear = CrearEstado(maquina, "Merodear", marcha, new Vector3(220, 180, 0), 1f);
        AnimatorState perseguir = CrearEstado(maquina, "Perseguir", marcha, new Vector3(440, 180, 0), 1.5f);
        AnimatorState capturaState = CrearEstado(maquina, "Captura", captura, new Vector3(660, 180, 0), 1f);
        maquina.defaultState = quieto;

        CrearTransicion(quieto, inicio, false, "InicioPersecucion", AnimatorConditionMode.If);
        CrearTransicion(quieto, merodear, false, "Caminando", AnimatorConditionMode.If,
            "Persiguiendo", AnimatorConditionMode.IfNot);
        CrearTransicion(inicio, perseguir, true, "Persiguiendo", AnimatorConditionMode.If);
        CrearTransicion(inicio, merodear, true, "Persiguiendo", AnimatorConditionMode.IfNot);
        CrearTransicion(inicio, quieto, false, "Caminando", AnimatorConditionMode.IfNot);
        CrearTransicion(merodear, inicio, false, "InicioPersecucion", AnimatorConditionMode.If);
        CrearTransicion(perseguir, merodear, false, "Persiguiendo", AnimatorConditionMode.IfNot);
        CrearTransicion(merodear, quieto, false, "Caminando", AnimatorConditionMode.IfNot);
        CrearTransicion(perseguir, quieto, false, "Caminando", AnimatorConditionMode.IfNot);

        AnimatorStateTransition transicionCaptura = maquina.AddAnyStateTransition(capturaState);
        transicionCaptura.hasExitTime = false;
        transicionCaptura.duration = 0.12f;
        transicionCaptura.hasFixedDuration = true;
        transicionCaptura.AddCondition(AnimatorConditionMode.If, 0f, "Capturar");

        EditorUtility.SetDirty(controlador);
        AssetDatabase.SaveAssets();
    }

    private static bool ControladorCorrecto(AnimatorController controlador, AnimationClip idle,
        AnimationClip inicioMarcha, AnimationClip marcha, AnimationClip captura)
    {
        AnimatorStateMachine maquina = controlador.layers[0].stateMachine;
        var estados = maquina.states.ToDictionary(child => child.state.name, child => child.state);
        return estados.Count == 5
            && estados.ContainsKey("Quieto") && estados["Quieto"].motion == idle
            && estados.ContainsKey("InicioMarcha") && estados["InicioMarcha"].motion == inicioMarcha
            && estados.ContainsKey("Merodear") && estados["Merodear"].motion == marcha
            && estados.ContainsKey("Perseguir") && estados["Perseguir"].motion == marcha
            && estados.ContainsKey("Captura") && estados["Captura"].motion == captura
            && controlador.parameters.Any(parameter => parameter.name == "Caminando")
            && controlador.parameters.Any(parameter => parameter.name == "Persiguiendo")
            && controlador.parameters.Any(parameter => parameter.name == "InicioPersecucion")
            && controlador.parameters.Any(parameter => parameter.name == "Capturar" && parameter.type == AnimatorControllerParameterType.Trigger);
    }

    private static AnimationClip ObtenerClip(string ruta)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ruta)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__", System.StringComparison.Ordinal));
    }

    private static AnimatorState CrearEstado(AnimatorStateMachine maquina, string nombre,
        Motion motion, Vector3 posicion, float velocidad)
    {
        AnimatorState estado = maquina.AddState(nombre, posicion);
        estado.motion = motion;
        estado.speed = velocidad;
        estado.writeDefaultValues = true;
        return estado;
    }

    private static void CrearTransicion(AnimatorState origen, AnimatorState destino, bool esperaSalida,
        params object[] condiciones)
    {
        AnimatorStateTransition transicion = origen.AddTransition(destino);
        transicion.hasExitTime = esperaSalida;
        transicion.exitTime = 0.95f;
        transicion.duration = 0.15f;
        transicion.hasFixedDuration = true;

        for (int i = 0; i < condiciones.Length; i += 2)
        {
            string nombre = (string)condiciones[i];
            AnimatorConditionMode modo = (AnimatorConditionMode)condiciones[i + 1];
            transicion.AddCondition(modo, 0f, nombre);
        }
    }

    private static void AsegurarParametro(List<AnimatorControllerParameter> parametros, string nombre,
        AnimatorControllerParameterType tipo, bool valorInicial)
    {
        AnimatorControllerParameter parametro = parametros.FirstOrDefault(item => item.name == nombre);
        if (parametro == null)
        {
            parametro = new AnimatorControllerParameter { name = nombre, type = tipo };
            parametros.Add(parametro);
        }

        parametro.type = tipo;
        if (tipo == AnimatorControllerParameterType.Bool)
            parametro.defaultBool = valorInicial;
    }
}

internal sealed class AnimacionesBichoPostprocessor : AssetPostprocessor
{
    private static readonly string[] RutasAnimacion =
    {
        "Assets/Bicho/Animaciones/Meshy_AI_Man_in_Light_Blue_Shi_1016161154_texture@Idle.fbx",
        "Assets/Bicho/Animaciones/Meshy_AI_Man_in_Light_Blue_Shi_1016161154_texture@Start Walking.fbx",
        "Assets/Bicho/Animaciones/Meshy_AI_Man_in_Light_Blue_Shi_1016161154_texture@Strut Walking.fbx",
        "Assets/Bicho/Animaciones/Meshy_AI_Man_in_Light_Blue_Shi_1016161154_texture@Searching Files High.fbx"
    };

    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (importedAssets.Any(path => RutasAnimacion.Contains(path)))
            EditorApplication.delayCall += ConfiguradorAnimacionesBicho.Configurar;
    }
}
