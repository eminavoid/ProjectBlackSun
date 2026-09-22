using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ZoneTwoMaterialSetup
{
    const string FbxPath = "Assets/TEXTURAS QUE SI SIRVEN/TEXTURAS QUE SI SIRVEN/MAPA CON TEXTURAS Y CASAS.fbx";
    const string PrefabPath = "Assets/3D/MAPA NUEVO/mapa ordenado .prefab";
    const string MeshDir = "Assets/3D/MAPA NUEVO/ZoneMeshes";
    const string MarkerName = "zone_two_materials_done.txt";

    struct ColorMats
    {
        public string CuadraGuid;
        public string CasaGuid;
    }

    static readonly Dictionary<string, ColorMats> ByColor = new Dictionary<string, ColorMats>
    {
        { "ZONE_RED", new ColorMats { CuadraGuid = "8c1a4e7b2d9f40a6b3c5d0e1f2a39480", CasaGuid = "7178e7702f73e22cab5eb3d5c4009a5a" } },
        { "ZONE_PINK", new ColorMats { CuadraGuid = "1d6b8e0c4a724f9e8b5c3d2a1f0e6971", CasaGuid = "a1850d27a7028424b0deeb603876f6e2" } },
        { "ZONE_GREEN", new ColorMats { CuadraGuid = "3f9a2c5d8e1b47c0a6d4e3f2b1c08956", CasaGuid = "a81f7ea3c46563516f696b61021bd8eb" } },
        { "ZONE_YELLOW", new ColorMats { CuadraGuid = "5a7e1d3c9b08426f8e2a4c6d0b1f3579", CasaGuid = "cdb19d7c927640f91e98f3d74f6fc773" } },
        { "ZONE_VIOLET", new ColorMats { CuadraGuid = "7b2c4e6a8d104f3b9c5e1a0d2f486791", CasaGuid = "06060f5534a78cc537f36f32f4864eb6" } },
        { "ZONE_WHITE", new ColorMats { CuadraGuid = "9d4f6a8c0e22415b7a3c5e9d1b086243", CasaGuid = "f4ed8aaeffbf029188b23be85de2a4fc" } },
    };

    [MenuItem("GameObject/Districts/Split Zones Into Cuadra And Casa", false, 11)]
    public static void RunFromMenu()
    {
        string marker = MarkerPath();
        if (File.Exists(marker)) File.Delete(marker);
        Run();
    }

    static bool running;

    public static void Run()
    {
        if (running) return;
        running = true;
        var log = new StringBuilder();
        try
        {
            EnsureReadable();
            int count = RebuildPrefab(log);
            log.AppendLine("ZONE_TWO_MATERIALS_OK zones=" + count);
            File.WriteAllText(LogPath(), log.ToString());
            File.WriteAllText(MarkerPath(), "ok");
            Debug.Log(log.ToString());
        }
        catch (Exception exception)
        {
            log.AppendLine("ZONE_TWO_MATERIALS_FAIL");
            log.AppendLine(exception.ToString());
            File.WriteAllText(LogPath(), log.ToString());
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            running = false;
        }
    }

    static void EnsureReadable()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("No se encontró el FBX del mapa.");
        if (importer.isReadable) return;
        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    static int RebuildPrefab(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder(MeshDir))
        {
            AssetDatabase.CreateFolder("Assets/3D/MAPA NUEVO", "ZoneMeshes");
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null) throw new InvalidOperationException("No se pudo cargar el FBX.");
        var sourceByName = new Dictionary<string, MeshRenderer>();
        MeshRenderer[] modelRenderers = model.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < modelRenderers.Length; i++)
        {
            sourceByName[modelRenderers[i].gameObject.name] = modelRenderers[i];
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        int count = 0;
        int missingHouses = 0;
        try
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (!TryGetColor(renderer.gameObject.name, out string colorKey)) continue;

                MeshRenderer sourceRenderer;
                if (!sourceByName.TryGetValue(renderer.gameObject.name, out sourceRenderer))
                {
                    throw new InvalidOperationException("El FBX no tiene " + renderer.gameObject.name);
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null)
                {
                    throw new InvalidOperationException(renderer.gameObject.name + " no tiene mesh.");
                }

                ColorMats paths = ByColor[colorKey];
                // Las texturas llamadas "casa" son el piso. El otro material es el de las casas.
                Material piso = LoadMaterial(paths.CasaGuid);
                Material casas = LoadMaterial(paths.CuadraGuid);
                if (piso == null || casas == null)
                {
                    throw new InvalidOperationException("Falta material para " + colorKey
                        + " piso=" + AssetDatabase.GUIDToAssetPath(paths.CasaGuid)
                        + " casas=" + AssetDatabase.GUIDToAssetPath(paths.CuadraGuid));
                }

                MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                Mesh source = sourceFilter.sharedMesh;
                if (source == null || !source.isReadable)
                {
                    throw new InvalidOperationException("Mesh no readable: " + renderer.gameObject.name);
                }

                Mesh split = BuildTwoSubmeshes(source, sourceRenderer.sharedMaterials, renderer.gameObject.name);
                string meshPath = MeshDir + "/" + renderer.gameObject.name + ".asset";
                Mesh saved = SaveMesh(split, meshPath);
                filter.sharedMesh = saved;
                renderer.sharedMaterials = new Material[] { piso, casas };
                int casaTris = saved.GetTriangles(1).Length / 3;
                if (casaTris == 0) missingHouses++;
                log.AppendLine(renderer.gameObject.name
                    + " pisoTris=" + saved.GetTriangles(0).Length / 3
                    + " casaTris=" + casaTris
                    + " src=" + DescribeSource(source, sourceRenderer.sharedMaterials)
                    + " mats=" + piso.name + " | " + casas.name);
                count++;
            }

            if (count == 0) throw new InvalidOperationException("No se encontraron zonas.");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (missingHouses > 0) log.AppendLine("WARNING zonas sin mesh de casa: " + missingHouses);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return count;
    }

    static Material LoadMaterial(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static Mesh BuildTwoSubmeshes(Mesh source, Material[] materials, string meshName)
    {
        var chunks = new List<int[]>();
        var houseFlags = new List<bool>();
        int largest = 0;
        int smallest = int.MaxValue;
        int smallestIndex = -1;
        for (int submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            int[] triangles = source.GetTriangles(submesh);
            if (triangles == null || triangles.Length == 0) continue;
            bool isFloorTexture = submesh < materials.Length && IsMisnamedFloorTexture(materials[submesh]);
            chunks.Add(triangles);
            houseFlags.Add(!isFloorTexture);
            if (triangles.Length > largest) largest = triangles.Length;
            if (triangles.Length < smallest)
            {
                smallest = triangles.Length;
                smallestIndex = chunks.Count - 1;
            }
        }

        int floorIndices = 0;
        int houseIndices = 0;
        for (int i = 0; i < chunks.Count; i++)
        {
            if (houseFlags[i]) houseIndices += chunks[i].Length;
            else floorIndices += chunks[i].Length;
        }

        // Si la textura de piso quedó en el mesh chico, el nombre estaba aplicado al revés.
        if (floorIndices > 0 && houseIndices > floorIndices * 4)
        {
            for (int i = 0; i < houseFlags.Count; i++) houseFlags[i] = !houseFlags[i];
        }

        bool anyHouse = false;
        for (int i = 0; i < houseFlags.Count; i++) if (houseFlags[i]) anyHouse = true;
        if (!anyHouse && smallestIndex >= 0 && smallest < 9000 && smallest * 8 < largest)
        {
            houseFlags[smallestIndex] = true;
        }

        var cuadra = new List<int>();
        var casa = new List<int>();
        for (int i = 0; i < chunks.Count; i++)
        {
            if (houseFlags[i]) casa.AddRange(chunks[i]);
            else cuadra.AddRange(chunks[i]);
        }

        Mesh mesh = new Mesh();
        mesh.name = meshName;
        mesh.indexFormat = source.vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.vertices = source.vertices;
        mesh.normals = source.normals;
        mesh.uv = source.uv;
        if (source.uv2 != null && source.uv2.Length == source.vertexCount) mesh.uv2 = source.uv2;
        if (source.tangents != null && source.tangents.Length == source.vertexCount) mesh.tangents = source.tangents;
        if (source.colors32 != null && source.colors32.Length == source.vertexCount) mesh.colors32 = source.colors32;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(cuadra, 0);
        mesh.SetTriangles(casa, 1);
        mesh.RecalculateBounds();
        return mesh;
    }

    static bool IsMisnamedFloorTexture(Material material)
    {
        if (material == null) return false;
        string name = material.name.ToLowerInvariant();
        return name.Contains("casa")
            || name.Contains("amarill")
            || name.Contains("roja")
            || name.Contains("rojo")
            || name.Contains("rosa")
            || name.Contains("verde")
            || name.Contains("violet")
            || name.Contains("blanca")
            || name.Contains("piso");
    }

    static string DescribeSource(Mesh source, Material[] materials)
    {
        var parts = new List<string>();
        for (int submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            int tris = source.GetTriangles(submesh).Length / 3;
            if (tris == 0) continue;
            string mat = submesh < materials.Length && materials[submesh] != null ? materials[submesh].name : "?";
            parts.Add(mat + ":" + tris);
        }
        return string.Join(", ", parts.ToArray());
    }

    static Mesh SaveMesh(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    static bool TryGetColor(string objectName, out string colorKey)
    {
        foreach (string key in ByColor.Keys)
        {
            if (objectName.StartsWith(key, StringComparison.Ordinal))
            {
                colorKey = key;
                return true;
            }
        }

        colorKey = null;
        return false;
    }

    static string MarkerPath()
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, MarkerName);
    }

    static string LogPath()
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "zone_two_materials_log.txt");
    }
}

[InitializeOnLoad]
static class ZoneTwoMaterialSetupBoot
{
    static ZoneTwoMaterialSetupBoot()
    {
        string marker = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "zone_two_materials_done.txt");
        if (File.Exists(marker)) return;
        EditorApplication.delayCall += TryRun;
    }

    static void TryRun()
    {
        string marker = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "zone_two_materials_done.txt");
        if (File.Exists(marker)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }

        ZoneTwoMaterialSetup.Run();
    }
}
