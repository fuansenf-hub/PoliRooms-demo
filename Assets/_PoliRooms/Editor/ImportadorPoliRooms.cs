using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace PoliRooms.Editor
{
    /// <summary>
    /// Configura automáticamente lo que se importa en _PoliRooms:
    ///  - Animaciones de Mixamo (Animaciones/*.fbx): Humanoid, con el movimiento de raíz "horneado"
    ///    (los personajes los mueve el código / NavMesh) y loop según el tipo.
    ///  - Audios (Audio/*): mono, para que el sonido 3D se ubique bien en el espacio.
    /// </summary>
    public class ImportadorPoliRooms : AssetPostprocessor
    {
        const string CarpetaAnim = "Assets/_PoliRooms/Animaciones/";
        const string CarpetaAudio = "Assets/_PoliRooms/Audio/";
        public const string CarpetaRiggeados = "Assets/_PoliRooms/Modelos/Riggeados/";

        static readonly HashSet<string> SinLoop = new HashSet<string> { "Levantarse", "EmpezarACaminar", "DejarDeCaminar" };

        void OnPreprocessModel()
        {
            if (assetPath.StartsWith(CarpetaRiggeados))
            {
                // Personajes riggeados con Mixamo: Humanoid para usar las animaciones compartidas.
                var pm = (ModelImporter)assetImporter;
                pm.animationType = ModelImporterAnimationType.Human;
                pm.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                pm.importAnimation = false;
                pm.materialImportMode = ModelImporterMaterialImportMode.None; // la textura la pone el constructor
                return;
            }
            if (!assetPath.StartsWith(CarpetaAnim)) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(CarpetaAnim)) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            string nombre = Path.GetFileNameWithoutExtension(assetPath);
            bool loop = !SinLoop.Contains(nombre);
            foreach (var c in clips)
            {
                c.name = nombre;
                c.loopTime = loop;
                c.loopPose = loop;
                // Raíz fija: rotación, altura y desplazamiento quedan en la pose.
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = false; // "Based Upon: Feet": los pies quedan en el piso
                c.heightFromFeet = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(CarpetaAudio)) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = true;
        }
    }
}
