using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Utility;
using System.IO;
using FullSerializer;

namespace RMBRocksMaterials
{
    [Serializable]
    public class MaterialDefinition
    {
        public int archive;
        public int record;
        public int frame;
    }

    [Serializable]
    public class ClimateMaterials
    {
        public MaterialDefinition[] defaultMaterials;
        public MaterialDefinition[] winterMaterials;
    }

    [Serializable]
    public class ClimateMaterialSettings
    {
        public ClimateMaterials ocean = new ClimateMaterials();
        public ClimateMaterials desert = new ClimateMaterials();
        public ClimateMaterials desert2 = new ClimateMaterials();
        public ClimateMaterials mountain = new ClimateMaterials();
        public ClimateMaterials rainforest = new ClimateMaterials();
        public ClimateMaterials swamp = new ClimateMaterials();
        public ClimateMaterials subtropical = new ClimateMaterials();
        public ClimateMaterials mountainWoods = new ClimateMaterials();
        public ClimateMaterials woodlands = new ClimateMaterials();
        public ClimateMaterials hauntedWoodlands = new ClimateMaterials();
        public ClimateMaterials mountainBalfiera = new ClimateMaterials();
        public ClimateMaterials mountainHammerfell = new ClimateMaterials();
    }

    [ImportedComponent]
    public class RMBRocksMaterials : MonoBehaviour
    {
        private ClimateMaterialSettings climateMaterialSettings;
        private MeshRenderer meshRenderer;
        private static readonly fsSerializer _serializer = new fsSerializer();

        static Mod mod;
        static bool WorldOfDaggerfallBiomesModEnabled = false;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            GameObject modGameObject = new GameObject(mod.Title);
            modGameObject.AddComponent<RMBRocksMaterials>();

            Mod worldOfDaggerfallBiomesMod = ModManager.Instance.GetModFromGUID("3b4319ac-34bb-411d-aa2c-d52b7b9eb69d");
            WorldOfDaggerfallBiomesModEnabled = worldOfDaggerfallBiomesMod != null && worldOfDaggerfallBiomesMod.Enabled;

            Debug.Log("RMBRocksMaterials: Init called.");
        }

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            Debug.Log($"[RMBRocksMaterials] Awake called for {gameObject.name}");
            LoadClimateMaterialSettings();
        }

        private void Start()
        {
            Debug.Log("[RMBRocksMaterials] Start called");
            UpdateMaterialBasedOnClimateAndSeason();
        }

        private void LoadClimateMaterialSettings()
        {
            string cleanName = gameObject.name.Replace("(Clone)", "").Replace(".prefab", "").Trim();
            Debug.Log($"[RMBRocksMaterials] Attempting to load JSON for '{cleanName}'");

            if (ModManager.Instance.TryGetAsset(cleanName + ".json", clone: false, out TextAsset jsonAsset))
            {
                string json = jsonAsset.text;
                Debug.Log($"[RMBRocksMaterials] JSON loaded successfully, contents: {json.Substring(0, Math.Min(json.Length, 500))}...");

                fsResult result = _serializer.TryDeserialize(fsJsonParser.Parse(json), ref climateMaterialSettings);
                if (!result.Succeeded)
                {
                    Debug.LogError($"[RMBRocksMaterials] Deserialization failed: {result.FormattedMessages}");
                }
                else
                {
                    Debug.Log("[RMBRocksMaterials] Deserialization succeeded");
                }
            }
            else
            {
                Debug.LogError("[RMBRocksMaterials] JSON file for material settings not found");
                climateMaterialSettings = new ClimateMaterialSettings(); // Fallback to default
            }
        }

        private Material[] LoadMaterialsFromDefinitions(MaterialDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
            {
                Debug.LogWarning("No definitions provided to LoadMaterialsFromDefinitions.");
                return new Material[0]; // Return an empty array.
            }

            if (DaggerfallUnity.Instance == null || DaggerfallUnity.Instance.MaterialReader == null)
            {
                Debug.LogError("DaggerfallUnity.Instance or MaterialReader is not initialized.");
                return null; // Return null or handle appropriately.
            }

            List<Material> materials = new List<Material>();
            foreach (var def in definitions)
            {
                Material loadedMaterial = null;
                Rect rectOut;

                // This is now safe to call after the null checks
                MaterialReader materialReader = DaggerfallUnity.Instance.MaterialReader;
                
                loadedMaterial = materialReader.GetMaterial(def.archive, def.record, def.frame, 0, out rectOut, 0, false, false);

                if (loadedMaterial == null)
                {
                    if (TextureReplacement.TryImportMaterial(def.archive, def.record, def.frame, out loadedMaterial))
                    {
                        materials.Add(loadedMaterial);
                    }
                    else
                    {
                        Debug.LogWarning($"Could not load material for archive: {def.archive}, record: {def.record}, frame: {def.frame}");
                        // Consider not adding nulls to the list to avoid potential issues downstream
                    }
                }
                else
                {
                    materials.Add(loadedMaterial);
                }
            }
            return materials.ToArray();
        }

        private ClimateMaterials GetMaterialsForClimate(MapsFile.Climates climate, bool isWinter)
        {
            ClimateMaterials selectedMaterials;

            // Restrict material selection when the "World of Daggerfall Biomes" mod is not enabled
            if (!WorldOfDaggerfallBiomesModEnabled)
            {
                switch (climate)
                {
                    case MapsFile.Climates.Desert:
                    case MapsFile.Climates.Mountain:
                    case MapsFile.Climates.Rainforest:
                    case MapsFile.Climates.Swamp:
                    case MapsFile.Climates.Woodlands:
                        selectedMaterials = climateMaterialSettings.GetType().GetField(climate.ToString().ToLower()).GetValue(climateMaterialSettings) as ClimateMaterials;
                        break;
                    default:
                        selectedMaterials = GetFallbackMaterialsForClimate(climate);
                        break;
                }
            }
            else
            {
                // If the mod is enabled, use the original climate material selection logic without restrictions
                selectedMaterials = climateMaterialSettings.GetType().GetField(climate.ToString().ToLower()).GetValue(climateMaterialSettings) as ClimateMaterials;
            }

            return selectedMaterials ?? climateMaterialSettings.woodlands; // Ensure a valid selection is always returned
        }

        private ClimateMaterials GetFallbackMaterialsForClimate(MapsFile.Climates climate)
        {
            switch (climate)
            {
                case MapsFile.Climates.HauntedWoodlands:
                    return climateMaterialSettings.woodlands;
                case MapsFile.Climates.Desert2:
                    return climateMaterialSettings.desert;
                case MapsFile.Climates.MountainWoods:
                    return climateMaterialSettings.mountain;
                case MapsFile.Climates.Ocean:
                case MapsFile.Climates.Subtropical:
                    // Assume a generalized fallback for climates not explicitly handled
                    return climateMaterialSettings.desert;
                default:
                    return climateMaterialSettings.woodlands; // Default fallback
            }
        }

        private void UpdateMaterialBasedOnClimateAndSeason()
        {
            MapsFile.Climates currentClimate = GetCurrentClimate();
            bool isWinter = IsWinter();
            string currentRegionName = GameManager.Instance.PlayerGPS.CurrentRegionName;

            // Start with the default materials for the current climate
            ClimateMaterials materialsForClimate = GetMaterialsForClimate(currentClimate, isWinter);

            // Adjust materials based on specific regions and their climates
            if (currentClimate == MapsFile.Climates.Mountain)
            {
                string[] hammerfellRegions = new string[] { "Alik'r Desert", "Dragontail Mountains", "Dak'fron", "Lainlyn", "Tigonus", "Ephesus", "Santaki" };
                string[] balfieraRegion = new string[] { "Isle of Balfiera" };

                // Check for Balfiera region and apply Balfiera mountains setting regardless of the mod status
                if (balfieraRegion.Contains(currentRegionName) && climateMaterialSettings.mountainBalfiera != null && (climateMaterialSettings.mountainBalfiera.defaultMaterials?.Length > 0 || climateMaterialSettings.mountainBalfiera.winterMaterials?.Length > 0))
                {
                    materialsForClimate = climateMaterialSettings.mountainBalfiera;
                }
                // Apply Hammerfell mountains setting only if the World of Daggerfall - Biomes mod is present
                else if (WorldOfDaggerfallBiomesModEnabled && hammerfellRegions.Contains(currentRegionName) && climateMaterialSettings.mountainHammerfell != null && (climateMaterialSettings.mountainHammerfell.defaultMaterials?.Length > 0 || climateMaterialSettings.mountainHammerfell.winterMaterials?.Length > 0))
                {
                    materialsForClimate = climateMaterialSettings.mountainHammerfell;
                }
            }

            // Load and apply materials based on the definitions
            MaterialDefinition[] definitions = isWinter ? materialsForClimate.winterMaterials : materialsForClimate.defaultMaterials;
            Material[] selectedMaterials = LoadMaterialsFromDefinitions(definitions);

            if (selectedMaterials != null && selectedMaterials.Length > 0 && meshRenderer != null)
            {
                meshRenderer.materials = selectedMaterials;
            }
            else
            {
                Debug.LogError("[RMBRocksMaterials] No valid materials found for the current climate and season.");
            }
        }

        private MapsFile.Climates GetCurrentClimate()
        {
            return (MapsFile.Climates)GameManager.Instance.PlayerGPS.CurrentClimateIndex;
        }

        private bool IsWinter()
        {
            DaggerfallDateTime now = DaggerfallUnity.Instance.WorldTime.Now;
            return now.SeasonValue == DaggerfallDateTime.Seasons.Winter;
        }
    }
}

