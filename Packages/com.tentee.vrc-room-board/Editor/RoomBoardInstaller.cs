#if UNITY_EDITOR
using RoomBoard;
using TMPro;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using VRC.Udon;

namespace RoomBoardEditor
{
    public static partial class RoomBoardInstaller
    {
        private const string PackageRootDir = "Packages/com.tentee.vrc-room-board/";
        private const string LegacyRootDir = "Assets/RoomBoard/";
        private static string RootDir { get { return AssetDatabase.IsValidFolder(PackageRootDir.TrimEnd('/')) ? PackageRootDir : LegacyRootDir; } }
        private static string RuntimeDir { get { return RootDir + "Runtime/"; } }
        private static string ManagerPath { get { return RuntimeDir + "RoomBoardManager"; } }
        private static string AreaPath { get { return RuntimeDir + "RoomBoardArea"; } }
        private static string DisplayPath { get { return RuntimeDir + "RoomBoardDisplay"; } }
        private static string FontDir { get { return RootDir + "Fonts/"; } }
        private static string FontSourcePath { get { return FontDir + "NotoSansJP-Regular.otf"; } }
        private static string FontAssetPath { get { return FontDir + "RoomBoard JP Dynamic.asset"; } }
        private static string GeneratedDir { get { return RootDir + "Generated/"; } }
        private static string UiMaterialPath { get { return GeneratedDir + "RoomBoard UI Material.mat"; } }
        private static string TmpMaterialPath { get { return GeneratedDir + "RoomBoard TMP Material.mat"; } }
        private static string PrefabDir { get { return RootDir + "Prefabs/"; } }
        private static string ManagerPrefabPath { get { return PrefabDir + "RoomBoard Manager.prefab"; } }
        private static string AreaPrefabPath { get { return PrefabDir + "RoomBoard Area.prefab"; } }
        private static string BoardPrefabPath { get { return PrefabDir + "RoomBoard Overview.prefab"; } }
        private static string DoorSignPrefabPath { get { return PrefabDir + "RoomBoard Door Sign.prefab"; } }
        private const string ExportPackagePath = "RoomBoard.unitypackage";
        private const string ManagerBuildRootName = "RoomBoard Manager (Build Temp)";
        private const string AreaBuildRootName = "RoomBoard Area (Build Temp)";
        private const string BoardBuildRootName = "RoomBoard Overview (Build Temp)";
        private const string DoorBuildRootName = "RoomBoard Door Sign (Build Temp)";

        private static readonly Color TokenBackground = new Color32(0x07, 0x13, 0x1B, 0xFF);
        private static readonly Color TokenSurface = new Color32(0x0F, 0x24, 0x31, 0xFF);
        private static readonly Color TokenText = new Color32(0xEA, 0xF4, 0xF5, 0xFF);
        private static readonly Color TokenMuted = new Color32(0x8F, 0xA8, 0xB3, 0xFF);
        private static readonly Color TokenAccent = new Color32(0x2E, 0xD3, 0xC6, 0xFF);
        private static readonly Color TokenIdle = new Color32(0x6B, 0x7A, 0x85, 0xFF);

        [MenuItem("Tools/TenteEEEE/Room Board/Build Prefabs")]
        public static void BuildPrefabsMenu()
        {
            EnsureProgramAssets();
            TMP_FontAsset font = EnsureFontAsset();
            EnsureDirectories();
            Material uiMaterial = EnsureUiMaterial(UiMaterialPath, "RoomBoard UI Material");
            Material tmpMaterial = EnsureTmpMaterial(font, TmpMaterialPath, "RoomBoard TMP Material");
            BuildManagerPrefab();
            BuildAreaPrefab();
            BuildDisplayPrefab(false, font, uiMaterial, tmpMaterial);
            BuildDisplayPrefab(true, font, uiMaterial, tmpMaterial);
            AssetDatabase.SaveAssets();
            Debug.Log("[RoomBoard] Build Prefabs complete.");
        }

        public static void BuildPrefabsBatch()
        {
            int exitCode = 0;
            try { BuildPrefabsMenu(); }
            catch (System.Exception e)
            {
                exitCode = 1;
                Debug.LogError("[RoomBoard] BuildPrefabsBatch failed: " + e);
            }
            EditorApplication.Exit(exitCode);
        }

        [MenuItem("Tools/TenteEEEE/Room Board/Install Sample into Current Scene")]
        public static void InstallSampleMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (Object.FindObjectsByType<RoomBoardManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0)
            {
                const string message = "このシーンには既に RoomBoardManager があります。サンプルは空のシーンで実行してください。";
                if (Application.isBatchMode) Debug.LogError("[RoomBoard] " + message);
                else EditorUtility.DisplayDialog("Room Board", message, "OK");
                return;
            }
            EnsureBuildAssetsAvailable();
            EnsureProgramsCompiled();
            GameObject managerObject = InstantiatePrefab(ManagerPrefabPath, scene, "Install Room Board Sample");
            RoomBoardManager manager = managerObject.GetComponent<RoomBoardManager>();
            GameObject areaAObject = InstantiatePrefab(AreaPrefabPath, scene, "Install Room Board Sample");
            GameObject areaBObject = InstantiatePrefab(AreaPrefabPath, scene, "Install Room Board Sample");
            areaAObject.name = "Room Board Area A";
            areaBObject.name = "Room Board Area B";
            areaAObject.transform.position = new Vector3(-5f, 0f, 0f);
            areaBObject.transform.position = new Vector3(5f, 0f, 0f);
            RoomBoardArea areaA = areaAObject.GetComponent<RoomBoardArea>();
            RoomBoardArea areaB = areaBObject.GetComponent<RoomBoardArea>();
            ConfigureArea(areaA, "部屋A", new Color32(0x2E, 0xD3, 0xC6, 0xFF), "Configure Room Board Area");
            ConfigureArea(areaB, "部屋B", new Color32(0xF4, 0xB9, 0x42, 0xFF), "Configure Room Board Area");
            ConfigureManager(manager, new RoomBoardArea[] { areaA, areaB }, "Configure Room Board Manager");

            GameObject board = InstantiatePrefab(BoardPrefabPath, scene, "Install Room Board Sample");
            board.name = "Room Board Overview";
            board.transform.position = new Vector3(0f, 1.5f, 0f);
            ConfigureDisplay(board.GetComponent<RoomBoardDisplay>(), manager, null, "Configure Room Board Overview");
            InstallDoorSign(DoorSignPrefabPath, scene, manager, areaA, new Vector3(-5f, 1.5f, -2.5f), "Room Board Door Sign A");
            InstallDoorSign(DoorSignPrefabPath, scene, manager, areaB, new Vector3(5f, 1.5f, -2.5f), "Room Board Door Sign B");
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[RoomBoard] Installed sample: Manager, 2 Areas, Board, and 2 Door Signs.");
        }

        [MenuItem("Tools/TenteEEEE/Room Board/Auto-Wire Current Scene")]
        public static void AutoWireMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            RoomBoardManager[] managers = Object.FindObjectsByType<RoomBoardManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (managers.Length != 1)
            {
                string text = "シーン内の RoomBoardManager は 1 つにしてください（現在 " + managers.Length + " 個）。";
                if (Application.isBatchMode) Debug.LogError("[RoomBoard] " + text);
                else EditorUtility.DisplayDialog("Room Board", text, "OK");
                return;
            }

            EnsureProgramsCompiled();
            RoomBoardManager manager = managers[0];
            RoomBoardArea[] areas = Object.FindObjectsByType<RoomBoardArea>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(areas, CompareHierarchyOrder);
            bool registeredAreas = false;
            if (manager.areas == null || manager.areas.Length == 0)
            {
                ConfigureManager(manager, areas, "Auto-Wire Room Board Manager");
                registeredAreas = true;
            }

            RoomBoardDisplay[] displays = Object.FindObjectsByType<RoomBoardDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int wiredDisplays = 0;
            for (int i = 0; i < displays.Length; i++)
            {
                if (displays[i].manager != null) continue;
                ConfigureDisplay(displays[i], manager, displays[i].areas, "Auto-Wire Room Board Display");
                wiredDisplays++;
            }

            int normalizedAreas = 0;
            for (int i = 0; i < areas.Length; i++)
            {
                BoxCollider[] colliders = CollectAreaColliders(areas[i]);
                bool changed = false;
                for (int c = 0; c < colliders.Length; c++)
                {
                    if (colliders[c].enabled || !colliders[c].isTrigger || colliders[c].gameObject.layer != 2) changed = true;
                }
                if (!changed) continue;
                Undo.RecordObject(areas[i], "Auto-Wire Room Board Area");
                for (int c = 0; c < colliders.Length; c++)
                {
                    Undo.RecordObject(colliders[c], "Auto-Wire Room Board Area");
                    Undo.RecordObject(colliders[c].gameObject, "Auto-Wire Room Board Area");
                    colliders[c].enabled = false;
                    colliders[c].isTrigger = true;
                    colliders[c].gameObject.layer = 2;
                    EditorUtility.SetDirty(colliders[c]);
                    EditorUtility.SetDirty(colliders[c].gameObject);
                }
                EditorUtility.SetDirty(areas[i]);
                normalizedAreas++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[RoomBoard] Auto-Wire complete. Registered manager areas: " + (registeredAreas ? areas.Length : 0) +
                "; displays assigned: " + wiredDisplays + "; areas with collider settings updated: " + normalizedAreas + ".");
        }

        // Own BoxColliders plus any volumes placed on other objects (e.g. the parts of an L-shaped room).
        private static BoxCollider[] CollectAreaColliders(RoomBoardArea area)
        {
            System.Collections.Generic.List<BoxCollider> list = new System.Collections.Generic.List<BoxCollider>(area.GetComponents<BoxCollider>());
            if (area.volumes != null)
            {
                for (int v = 0; v < area.volumes.Length; v++)
                {
                    if (area.volumes[v] != null && !list.Contains(area.volumes[v])) list.Add(area.volumes[v]);
                }
            }
            return list.ToArray();
        }

        [MenuItem("Tools/TenteEEEE/Room Board/Export UnityPackage")]
        public static void ExportUnityPackageMenu()
        {
            AssetDatabase.ExportPackage(RootDir.TrimEnd('/'), ExportPackagePath, ExportPackageOptions.Recurse);
            Debug.Log("[RoomBoard] Exported " + ExportPackagePath);
        }

        public static void ExportUnityPackageBatch()
        {
            int exitCode = 0;
            try { ExportUnityPackageMenu(); }
            catch (System.Exception e) { exitCode = 1; Debug.LogError("[RoomBoard] Export failed: " + e); }
            EditorApplication.Exit(exitCode);
        }

        private static void EnsureBuildAssetsAvailable()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(AreaPrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(BoardPrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(DoorSignPrefabPath) != null &&
                AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ManagerPath + ".asset") != null &&
                AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(AreaPath + ".asset") != null &&
                AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(DisplayPath + ".asset") != null) return;
            BuildPrefabsMenu();
        }

        // The shipped program assets point at compiled programs that exist only in the authoring
        // project. Until UdonSharp compiles in the user's project, writing fields through a proxy
        // throws, so compile synchronously before any menu that configures behaviours.
        private static void EnsureProgramsCompiled()
        {
            if (AllProgramsCompiled()) return;
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            if (!AllProgramsCompiled())
                throw new System.InvalidOperationException("[RoomBoard] UdonSharp programs are not compiled. Fix any UdonSharp compile errors in the Console, then run this menu item again.");
        }

        private static bool AllProgramsCompiled()
        {
            string[] paths = { ManagerPath, AreaPath, DisplayPath };
            for (int i = 0; i < paths.Length; i++)
            {
                UdonSharpProgramAsset asset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(paths[i] + ".asset");
                if (asset == null || asset.GetRealProgram() == null) return false;
            }
            return true;
        }

        private static void EnsureProgramAssets()
        {
            bool created = EnsureProgramAsset(ManagerPath) | EnsureProgramAsset(AreaPath) | EnsureProgramAsset(DisplayPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            AssetDatabase.SaveAssets();
            if (UdonSharpProgramAsset.GetProgramAssetForClass(typeof(RoomBoardManager)) == null ||
                UdonSharpProgramAsset.GetProgramAssetForClass(typeof(RoomBoardArea)) == null ||
                UdonSharpProgramAsset.GetProgramAssetForClass(typeof(RoomBoardDisplay)) == null)
                throw new System.InvalidOperationException("[RoomBoard] Failed to associate one or more UdonSharp program assets.");
            if (created)
                throw new System.InvalidOperationException("[RoomBoard] Created a UdonSharp program asset for the first time. Run this menu item once more to install.");
        }

        private static bool EnsureProgramAsset(string pathWithoutExtension)
        {
            string scriptPath = pathWithoutExtension + ".cs";
            string assetPath = pathWithoutExtension + ".asset";
            MonoScript sourceScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (sourceScript == null) throw new System.InvalidOperationException("[RoomBoard] Missing UdonSharp source: " + scriptPath);
            UdonSharpProgramAsset asset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                asset.sourceCsScript = sourceScript;
                AssetDatabase.CreateAsset(asset, assetPath);
                return true;
            }
            if (asset.sourceCsScript != sourceScript) { asset.sourceCsScript = sourceScript; EditorUtility.SetDirty(asset); }
            return false;
        }

        private static TMP_FontAsset EnsureFontAsset()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            if (sourceFont == null) throw new System.InvalidOperationException("[RoomBoard] Missing required font source: " + FontSourcePath);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                if (Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
                    throw new System.InvalidOperationException("[RoomBoard] TMP Essential Resources are missing. Import them, then run Build Prefabs again.");
                fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                fontAsset.name = "RoomBoard JP Dynamic";
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            }
            if (fontAsset.sourceFontFile == null)
                throw new System.InvalidOperationException("[RoomBoard] Dynamic font has no sourceFontFile reference: " + FontAssetPath);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.ClearFontAssetData(true);
            // Glyphs the editor adds while previewing must not ship, and the build must start from
            // an empty atlas. The property setter is internal, so go through the serialized field.
            SerializedObject serializedFont = new SerializedObject(fontAsset);
            serializedFont.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            serializedFont.ApplyModifiedPropertiesWithoutUndo();
            AddFontSubAssets(fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static void AddFontSubAssets(TMP_FontAsset fontAsset)
        {
            Texture2D[] atlases = fontAsset.atlasTextures;
            if (atlases != null)
                for (int i = 0; i < atlases.Length; i++)
                    if (atlases[i] != null && !AssetDatabase.IsSubAsset(atlases[i])) { atlases[i].name = "RoomBoard JP Atlas " + i; AssetDatabase.AddObjectToAsset(atlases[i], fontAsset); }
            if (fontAsset.material != null && !AssetDatabase.IsSubAsset(fontAsset.material))
            {
                fontAsset.material.name = "RoomBoard JP Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
        }

        private static Material EnsureUiMaterial(string path, string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("UI/Default");
                if (shader == null) throw new System.InvalidOperationException("[RoomBoard] Missing built-in UI/Default shader.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.renderQueue = 3000;
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureTmpMaterial(TMP_FontAsset font, string path, string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (font == null || font.material == null) throw new System.InvalidOperationException("[RoomBoard] Missing TMP font material source.");
                material = new Material(font.material) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.renderQueue = 3000;
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder(RootDir.TrimEnd('/'))) throw new System.InvalidOperationException("[RoomBoard] Missing root folder: " + RootDir);
            if (!AssetDatabase.IsValidFolder(PrefabDir.TrimEnd('/'))) AssetDatabase.CreateFolder(RootDir.TrimEnd('/'), "Prefabs");
            if (!AssetDatabase.IsValidFolder(GeneratedDir.TrimEnd('/'))) AssetDatabase.CreateFolder(RootDir.TrimEnd('/'), "Generated");
        }

        private static void BuildManagerPrefab()
        {
            DestroyLeftoverBuildRoot(ManagerBuildRootName);
            GameObject root = new GameObject(ManagerBuildRootName);
            RoomBoardManager proxy = root.AddUdonSharpComponent<RoomBoardManager>();
            proxy.areas = new RoomBoardArea[0];
            proxy.listeners = new UdonBehaviour[0];
            UdonSharpEditorUtility.CopyProxyToUdon(proxy);
            SavePrefab(root, ManagerPrefabPath, "RoomBoard Manager");
        }

        private static void BuildAreaPrefab()
        {
            DestroyLeftoverBuildRoot(AreaBuildRootName);
            GameObject root = new GameObject(AreaBuildRootName);
            root.layer = 2;
            RoomBoardArea proxy = root.AddUdonSharpComponent<RoomBoardArea>();
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(4f, 3f, 4f);
            box.center = new Vector3(0f, 1.5f, 0f);
            box.enabled = false;
            box.isTrigger = true;
            proxy.areaName = "Room";
            proxy.areaColor = TokenAccent;
            proxy.volumes = new BoxCollider[] { box };
            UdonSharpEditorUtility.CopyProxyToUdon(proxy);
            SavePrefab(root, AreaPrefabPath, "RoomBoard Area");
        }

        private static void BuildDisplayPrefab(bool doorSign, TMP_FontAsset font, Material uiMaterial, Material tmpMaterial)
        {
            string tempName = doorSign ? DoorBuildRootName : BoardBuildRootName;
            string prefabPath = doorSign ? DoorSignPrefabPath : BoardPrefabPath;
            DestroyLeftoverBuildRoot(tempName);
            GameObject root = new GameObject(tempName);
            RoomBoardDisplay proxy = root.AddUdonSharpComponent<RoomBoardDisplay>();
            proxy.manager = null;
            proxy.areas = doorSign ? new RoomBoardArea[1] : new RoomBoardArea[0];
            proxy.boardTitle = doorSign ? "" : "在室状況";
            // The sign fits about three lines; past that the rest must read as "ほか N 人", not vanish into an ellipsis.
            proxy.maxNamesPerArea = doorSign ? 4 : 12;
            proxy.textScale = 1f;
            proxy.highlightSeconds = 5f;
            proxy.nameSeparator = "　";

            float width = doorSign ? 350f : 900f;
            float scale = 0.001f;
            Canvas canvas = CreateCanvas(root.transform, "Canvas", new Vector2(width, 0f), scale);
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.pivot = new Vector2(0.5f, 1f);
            GameObject panelObject = CreatePanel(canvas.transform, "Panel", width, 0f, TokenBackground);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup panelLayout = panelObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(14, 14, 12, 12);
            panelLayout.spacing = 8f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;
            ContentSizeFitter panelFitter = panelObject.AddComponent<ContentSizeFitter>();
            panelFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI title = CreatePanelTitle(panelObject.transform, "Title", "在室状況", 30f,
                width - 32f, 48f, font, TokenText);
            if (doorSign) title.gameObject.SetActive(false);

            GameObject rowContainerObject = new GameObject("Row Container", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rowContainerObject.transform.SetParent(panelObject.transform, false);
            RectTransform rowContainerRect = rowContainerObject.GetComponent<RectTransform>();
            rowContainerRect.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup rowsLayout = rowContainerObject.GetComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 7f;
            rowsLayout.childAlignment = TextAnchor.UpperCenter;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;
            GameObject template = CreatePresenceRow(rowContainerObject.transform, "Row Template", width - 32f,
                doorSign ? 150f : 100f, doorSign ? 26f : 20f, doorSign ? 22f : 18f, font);
            template.SetActive(false);

            proxy.titleText = title;
            proxy.rowContainer = rowContainerRect;
            proxy.rowTemplate = template;
            ApplyMaterials(root, uiMaterial, tmpMaterial);
            UdonSharpEditorUtility.CopyProxyToUdon(proxy);
            SavePrefab(root, prefabPath, doorSign ? "RoomBoard Door Sign" : "RoomBoard Overview");
        }

        private static void ApplyMaterials(GameObject root, Material uiMaterial, Material tmpMaterial)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++) images[i].material = uiMaterial;
            TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++) { texts[i].font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath); texts[i].fontSharedMaterial = tmpMaterial; }
        }

        private static GameObject InstantiatePrefab(string path, Scene scene, string undoName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new System.InvalidOperationException("[RoomBoard] Missing prefab: " + path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, undoName);
            return instance;
        }

        private static void InstallDoorSign(string prefabPath, Scene scene, RoomBoardManager manager,
            RoomBoardArea area, Vector3 position, string name)
        {
            GameObject sign = InstantiatePrefab(prefabPath, scene, "Install Room Board Sample");
            sign.name = name;
            sign.transform.position = position;
            ConfigureDisplay(sign.GetComponent<RoomBoardDisplay>(), manager, new RoomBoardArea[] { area }, "Configure Room Board Door Sign");
        }

        private static void ConfigureArea(RoomBoardArea area, string name, Color color, string undoName)
        {
            if (area == null) return;
            Undo.RecordObject(area, undoName);
            area.areaName = name;
            area.areaColor = color;
            BoxCollider[] colliders = area.GetComponents<BoxCollider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Undo.RecordObject(colliders[i], undoName);
                Undo.RecordObject(colliders[i].gameObject, undoName);
                colliders[i].enabled = false;
                colliders[i].isTrigger = true;
                colliders[i].gameObject.layer = 2;
                EditorUtility.SetDirty(colliders[i].gameObject);
            }
            if (colliders.Length > 0) area.volumes = new BoxCollider[] { colliders[0] };
            UdonSharpEditorUtility.CopyProxyToUdon(area);
            EditorUtility.SetDirty(area);
        }

        private static void ConfigureManager(RoomBoardManager manager, RoomBoardArea[] areas, string undoName)
        {
            if (manager == null) return;
            Undo.RecordObject(manager, undoName);
            manager.areas = areas;
            if (manager.listeners == null) manager.listeners = new UdonBehaviour[0];
            UdonSharpEditorUtility.CopyProxyToUdon(manager);
            EditorUtility.SetDirty(manager);
        }

        private static void ConfigureDisplay(RoomBoardDisplay display, RoomBoardManager manager, RoomBoardArea[] areas, string undoName)
        {
            if (display == null) return;
            Undo.RecordObject(display, undoName);
            display.manager = manager;
            display.areas = areas ?? new RoomBoardArea[0];
            UdonSharpEditorUtility.CopyProxyToUdon(display);
            EditorUtility.SetDirty(display);
        }

        private static int CompareHierarchyOrder(RoomBoardArea a, RoomBoardArea b)
        {
            string pathA = GetHierarchyPath(a == null ? null : a.transform);
            string pathB = GetHierarchyPath(b == null ? null : b.transform);
            return string.CompareOrdinal(pathA, pathB);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null) return "";
            string path = transform.GetSiblingIndex().ToString("D6");
            while (transform.parent != null) { transform = transform.parent; path = transform.GetSiblingIndex().ToString("D6") + "/" + path; }
            return path;
        }

        private static void SavePrefab(GameObject root, string path, string finalName)
        {
            root.name = finalName;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void DestroyLeftoverBuildRoot(string name)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded) return;
            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i].name == name) Object.DestroyImmediate(roots[i]);
        }
    }
}
#endif
