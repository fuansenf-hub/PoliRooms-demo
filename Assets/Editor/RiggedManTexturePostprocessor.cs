using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the matching Meshy OBJ albedo texture to materials imported from the rigged FBX.
/// </summary>
public sealed class RiggedManTexturePostprocessor : AssetPostprocessor
{
    private const string ModelPath = "Assets/Bicho/Modelos/ModeloNuevo/rigged-man-in-light-blue-shirt.fbx";
    private const string TexturePath = "Assets/Bicho/Modelos/ModeloNuevo/Meshy_AI_Man_in_Light_Blue_Shi_1016161154_texture_obj/Meshy_AI_Man_in_Light_Blue_Shi_1016161154_texture_obj/Meshy_AI_Man_in_Light_Blue_Shi_1016161154_texture.png";

    public override uint GetVersion() => 1;

    private void OnPostprocessMaterial(Material material)
    {
        if (assetPath != ModelPath || material == null)
            return;

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (albedo == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", albedo);

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", albedo);
    }
}


