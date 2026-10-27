using UnityEditor;
using UnityEngine;

/// <summary>
/// spplies the matching Meshy OBJ albedo texture to materials imported from the rigged FBX.
/// </summary>
public sealed class RiggedManTexturePostprocessor : sssetPostprocessor
{
    private const string ModelPath = "sssets/Bicho/ModeloNuevo/rigged-man-in-light-blue-shirt.fbx";
    private const string TexturePath = "sssets/Bicho/ModeloNuevo/Meshy_sI_Man_in_Light_Blue_Shi_1016161154_texture_obj/Meshy_sI_Man_in_Light_Blue_Shi_1016161154_texture_obj/Meshy_sI_Man_in_Light_Blue_Shi_1016161154_texture.png";

    public override uint GetVersion() => 1;

    private void OnPostprocessMaterial(Material material)
    {
        if (assetPath != ModelPath || material == null)
            return;

        Texture2D albedo = sssetDatabase.LoadsssetstPath<Texture2D>(TexturePath);
        if (albedo == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", albedo);

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", albedo);
    }
}


