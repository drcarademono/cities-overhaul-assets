using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Utility;

[ImportedComponent]
public class RMBCropBillboard : MonoBehaviour
{
    private Camera mainCamera;
    static Mod mod;
    public bool FaceY = false; // Set this based on whether you want the object to rotate around Y axis only
    private MeshRenderer meshRenderer;

    [Invoke(StateManager.StateTypes.Start, 0)]
    public static void Init(InitParams initParams)
    {
        mod = initParams.Mod;
        GameObject modGameObject = new GameObject(mod.Title);
        modGameObject.AddComponent<RMBCropBillboard>();
        Debug.Log("RMBCropBillboard: Init called and component added to game object.");
    }

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (mainCamera == null)
        {
            mainCamera = Camera.main; // Automatically find the main camera if not assigned
        }
        ApplyMaterialBasedOnName();
    }

    void Update()
    {
        if (mainCamera && Application.isPlaying && meshRenderer.enabled)
        {
            float y = FaceY ? mainCamera.transform.forward.y : 0;
            Vector3 viewDirection = -new Vector3(mainCamera.transform.forward.x, y, mainCamera.transform.forward.z);
            transform.LookAt(transform.position + viewDirection);
        }
    }

    private void ApplyMaterialBasedOnName()
    {
        string name = gameObject.name;
        Debug.Log($"Checking object with name: {name}");
        if (name.StartsWith("DaggerfallBillboard"))
        {
            Debug.Log("Object is a Daggerfall billboard.");
            string[] parts = name.Split(new[] { "__" }, System.StringSplitOptions.None);
            if (parts.Length >= 3)
            {
                MeshFilter meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    Debug.LogError("MeshFilter component not found on the billboard object.");
                    return;
                }

                // Get the original height before applying new mesh
                float originalHeight = meshFilter.mesh ? meshFilter.mesh.bounds.size.y : 0;

                int archive;
                int record;
                if (IsWinter())
                {
                    archive = 511; // Use winter archive
                    record = 22;  // Use winter record
                }
                else
                {
                    archive = 301; // Default archive
                    int initialRecord = int.Parse(parts[2].Split('_')[1]);
                    record = AdjustRecordBasedOnClimate(initialRecord);
                }
                Debug.Log($"Using archive: {archive}, record: {record} based on season and climate.");

                MaterialReader materialReader = DaggerfallUnity.Instance.MaterialReader;
                MeshReader meshReader = DaggerfallUnity.Instance.MeshReader;
                Rect rectOut;
                Vector2 sizeOut;
                Material material = materialReader.GetMaterial(archive, record, 0, 0, out rectOut, 4, true, true);
                Mesh mesh = meshReader.GetBillboardMesh(rectOut, archive, record, out sizeOut);

                if (material != null && mesh != null)
                {
                    Renderer renderer = GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material = material;
                        meshFilter.mesh = mesh;
                        Debug.Log("Material and mesh successfully applied to renderer.");
                        
                        // Calculate new height and adjust position to align the base
                        float newHeight = mesh.bounds.size.y;
                        float heightDifference = newHeight - originalHeight;
                        AlignToBase(transform.position.y + heightDifference / 2);
                    }
                    else
                    {
                        Debug.LogError("Renderer component not found on the billboard object.");
                    }
                }
                else
                {
                    Debug.LogError($"Failed to load material or mesh for archive: {archive}, record: {record}");
                }
            }
            else
            {
                Debug.LogError("Object name does not have enough parts to extract material info.");
            }
        }
        else
        {
            Debug.Log("Object name does not start with 'DaggerfallBillboard' and will not be processed.");
        }
    }

    private int AdjustRecordBasedOnClimate(int initialRecord)
    {
        MapsFile.Climates currentClimate = GetCurrentClimate();
        switch (currentClimate)
        {
            case MapsFile.Climates.Mountain:
                return initialRecord == 0 ? 19 : 21;
            case MapsFile.Climates.Desert:
            case MapsFile.Climates.Desert2:
                return 2;
            case MapsFile.Climates.Subtropical:
                return initialRecord == 0 ? 3 : 4;
            case MapsFile.Climates.Rainforest:
            case MapsFile.Climates.Swamp:
                return initialRecord == 0 ? 7 : 8;
            default:
                return initialRecord; // No change for Temperate, MountainWoods, HauntedWoodlands
        }
    }

    private void AlignToBase(float newYPosition)
    {
        Vector3 newPosition = transform.position;
        newPosition.y = newYPosition; // Update to align base properly
        transform.position = newPosition;
        Debug.Log("Billboard aligned to maintain base position.");
    }

    private MapsFile.Climates GetCurrentClimate()
    {
        return (MapsFile.Climates)GameManager.Instance.PlayerGPS.CurrentClimateIndex;
    }

    /// <summary>
    /// Checks if current season is winter and player is not in a desert region.
    /// </summary>
    /// <returns>True if is winter.</returns>
    private static bool IsWinter()
    {
        return GameManager.Instance.PlayerGPS.CurrentClimateIndex != (int)MapsFile.Climates.Desert &&
        GameManager.Instance.PlayerGPS.CurrentClimateIndex != (int)MapsFile.Climates.Desert2 &&
        DaggerfallUnity.Instance.WorldTime.Now.SeasonValue == DaggerfallDateTime.Seasons.Winter;
    }
}

