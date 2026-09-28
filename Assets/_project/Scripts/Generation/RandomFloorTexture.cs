using UnityEngine;

// RandomFloorTexture - assigns a random texture (from a fixed pool) to this room's floor
// MeshRenderer, once, when the room is instantiated.
// Each room instance gets its OWN material instance (via Renderer.material, NOT sharedMaterial) so different rooms can show different
// textures simultaneously without affecting each other or the original shared Material asset
[RequireComponent(typeof(MeshRenderer))]
public class RandomFloorTexture : MonoBehaviour
{
    [Header("Texture Pool")]
    [SerializeField] private Texture2D[] floorTextures;

    [Header("Shader Property")]
    [SerializeField] private string mainTexturePropertyName = "_MainTex";

    [Header("Tiling")]
    [Tooltip("How many times the texture repeats across the mesh (X, Y)")]
    [SerializeField] private Vector2 textureTiling = new Vector2(4f, 4f);

    private MeshRenderer meshRenderer;

    private bool hasAppliedTexture = false;

    private void LateUpdate()
    {
        if(hasAppliedTexture)
        {
            return;
        }

        hasAppliedTexture = true;

        if(meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        ApplyRandomTexture();
    }


    private void ApplyRandomTexture()
    {
        if(floorTextures == null || floorTextures.Length == 0)
        {
            Debug.LogWarning(
                name + ": RandomFloorTexture has no textures assigned in floorTextures - " +
                "the floor will keep whatever texture its base Material already has.",
                this
            );

            return;
        }

        Texture2D chosenTexture = floorTextures[Random.Range(0, floorTextures.Length)];

        if(chosenTexture == null)
        {
            Debug.LogWarning(
                name + ": floorTextures contains a null entry - skipping texture assignment " +
                "for this room's floor.",
                this
            );

            return;
        }

        // Accessing .material for irst time automatically instantiates a per-object COPY of whatever material was
        // assigned in the Inspector, so setting a texture on it only affects THIS room's floor
        Material instancedMaterial = meshRenderer.material;

        if(!instancedMaterial.HasProperty(mainTexturePropertyName))
        {
            Debug.LogError(
                name + ": material's shader has no property named \"" + mainTexturePropertyName +
                "\" - cannot assign texture. Check your shader's actual main texture property " +
                "name (commonly \"_MainTex\" for Built-in/Standard, \"_BaseMap\" for URP) and " +
                "update mainTexturePropertyName in the Inspector.",
                this
            );

            return;
        }

        instancedMaterial.SetTexture(mainTexturePropertyName, chosenTexture);
        instancedMaterial.SetTextureScale(mainTexturePropertyName, textureTiling);
    }
}