#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class SliceSpriteSheets : EditorWindow
{
    private const float SPACER = 10.0f;
    private const string PREFS_PREFIX = "SliceSpriteSheets_";

    // -------------------------------------------------------------------------
    // Data
    // -------------------------------------------------------------------------

    [System.Serializable]
    public class SlicingJob
    {
        public Texture2D texture;
        public bool isEnabled = true;
        public SliceMode sliceMode = SliceMode.CellSize;
        public int cellsPerRow = 4;
        public int cellsPerColumn = 4;
        public int cellWidth = 32;
        public int cellHeight = 32;
        public SpriteAlignment pivot = SpriteAlignment.Center;
        public Vector2 customPivot = new Vector2(0.5f, 0.5f);
        public bool ignoreEmpty = true;
        public string namePattern = "{texture}_{index}"; // supports {texture}, {row}, {col}, {index}
        public int paddingX = 0;
        public int paddingY = 0;
        public int offsetX = 0;
        public int offsetY = 0;
        public bool autoDetected = false;
    }

    public enum SliceMode { CellCount, CellSize }

    // -------------------------------------------------------------------------
    // Global settings (persisted via EditorPrefs)
    // -------------------------------------------------------------------------

    private SliceMode globalSliceMode = SliceMode.CellSize;
    private int globalCellsPerRow = 4;
    private int globalCellsPerColumn = 4;
    private int globalCellWidth = 32;
    private int globalCellHeight = 32;
    private SpriteAlignment globalPivot = SpriteAlignment.Center;
    private Vector2 globalCustomPivot = new Vector2(0.5f, 0.5f);
    private bool globalIgnoreEmpty = true;
    private string globalNamePattern = "{texture}_{index}";
    private int globalPaddingX = 0;
    private int globalPaddingY = 0;
    private int globalOffsetX = 0;
    private int globalOffsetY = 0;

    // -------------------------------------------------------------------------
    // Auto-refresh
    // -------------------------------------------------------------------------

    private bool autoRefresh = false;
    private float autoRefreshInterval = 0.5f;
    private double lastRefreshTime = 0.0;
    private bool isProcessing = false;

    // -------------------------------------------------------------------------
    // Selection cache (avoids redundant refreshes)
    // -------------------------------------------------------------------------

    private HashSet<string> cachedSelectionPaths = new HashSet<string>();

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private Vector2 scrollPosition;
    private List<SlicingJob> activeJobs = new List<SlicingJob>();

    // Pivot lookup — replaces the magic-math AlignToPivot
    private static readonly Dictionary<SpriteAlignment, Vector2> PivotMap = new Dictionary<SpriteAlignment, Vector2>
    {
        { SpriteAlignment.TopLeft,      new Vector2(0.0f, 1.0f) },
        { SpriteAlignment.TopCenter,    new Vector2(0.5f, 1.0f) },
        { SpriteAlignment.TopRight,     new Vector2(1.0f, 1.0f) },
        { SpriteAlignment.LeftCenter,   new Vector2(0.0f, 0.5f) },
        { SpriteAlignment.Center,       new Vector2(0.5f, 0.5f) },
        { SpriteAlignment.RightCenter,  new Vector2(1.0f, 0.5f) },
        { SpriteAlignment.BottomLeft,   new Vector2(0.0f, 0.0f) },
        { SpriteAlignment.BottomCenter, new Vector2(0.5f, 0.0f) },
        { SpriteAlignment.BottomRight,  new Vector2(1.0f, 0.0f) },
        { SpriteAlignment.Custom,       new Vector2(0.5f, 0.5f) },
    };

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    [MenuItem("Tools/Slice Sprite Sheets")]
    public static void ShowWindow() => GetWindow<SliceSpriteSheets>("Slice Sprite Sheets");

    private void OnEnable()
    {
        LoadPrefs();
        EditorApplication.update += UpdateAutoRefresh;
        RefreshSelectedSpriteSheets();
    }

    private void OnDisable()
    {
        SavePrefs();
        EditorApplication.update -= UpdateAutoRefresh;
    }

    // -------------------------------------------------------------------------
    // EditorPrefs persistence
    // -------------------------------------------------------------------------

    private void SavePrefs()
    {
        EditorPrefs.SetInt(PREFS_PREFIX + "sliceMode",       (int)globalSliceMode);
        EditorPrefs.SetInt(PREFS_PREFIX + "cellsPerRow",     globalCellsPerRow);
        EditorPrefs.SetInt(PREFS_PREFIX + "cellsPerColumn",  globalCellsPerColumn);
        EditorPrefs.SetInt(PREFS_PREFIX + "cellWidth",       globalCellWidth);
        EditorPrefs.SetInt(PREFS_PREFIX + "cellHeight",      globalCellHeight);
        EditorPrefs.SetInt(PREFS_PREFIX + "pivot",           (int)globalPivot);
        EditorPrefs.SetFloat(PREFS_PREFIX + "customPivotX",  globalCustomPivot.x);
        EditorPrefs.SetFloat(PREFS_PREFIX + "customPivotY",  globalCustomPivot.y);
        EditorPrefs.SetBool(PREFS_PREFIX + "ignoreEmpty",    globalIgnoreEmpty);
        EditorPrefs.SetString(PREFS_PREFIX + "namePattern",  globalNamePattern);
        EditorPrefs.SetInt(PREFS_PREFIX + "paddingX",        globalPaddingX);
        EditorPrefs.SetInt(PREFS_PREFIX + "paddingY",        globalPaddingY);
        EditorPrefs.SetInt(PREFS_PREFIX + "offsetX",         globalOffsetX);
        EditorPrefs.SetInt(PREFS_PREFIX + "offsetY",         globalOffsetY);
        EditorPrefs.SetBool(PREFS_PREFIX + "autoRefresh",    autoRefresh);
        EditorPrefs.SetFloat(PREFS_PREFIX + "refreshInterval", autoRefreshInterval);
    }

    private void LoadPrefs()
    {
        globalSliceMode      = (SliceMode)EditorPrefs.GetInt(PREFS_PREFIX + "sliceMode",      (int)SliceMode.CellSize);
        globalCellsPerRow    = EditorPrefs.GetInt(PREFS_PREFIX + "cellsPerRow",    4);
        globalCellsPerColumn = EditorPrefs.GetInt(PREFS_PREFIX + "cellsPerColumn", 4);
        globalCellWidth      = EditorPrefs.GetInt(PREFS_PREFIX + "cellWidth",      32);
        globalCellHeight     = EditorPrefs.GetInt(PREFS_PREFIX + "cellHeight",     32);
        globalPivot          = (SpriteAlignment)EditorPrefs.GetInt(PREFS_PREFIX + "pivot", (int)SpriteAlignment.Center);
        globalCustomPivot    = new Vector2(EditorPrefs.GetFloat(PREFS_PREFIX + "customPivotX", 0.5f),
                                           EditorPrefs.GetFloat(PREFS_PREFIX + "customPivotY", 0.5f));
        globalIgnoreEmpty    = EditorPrefs.GetBool(PREFS_PREFIX + "ignoreEmpty",   true);
        globalNamePattern    = EditorPrefs.GetString(PREFS_PREFIX + "namePattern", "{texture}_{index}");
        globalPaddingX       = EditorPrefs.GetInt(PREFS_PREFIX + "paddingX",       0);
        globalPaddingY       = EditorPrefs.GetInt(PREFS_PREFIX + "paddingY",       0);
        globalOffsetX        = EditorPrefs.GetInt(PREFS_PREFIX + "offsetX",        0);
        globalOffsetY        = EditorPrefs.GetInt(PREFS_PREFIX + "offsetY",        0);
        autoRefresh          = EditorPrefs.GetBool(PREFS_PREFIX + "autoRefresh",   false);
        autoRefreshInterval  = EditorPrefs.GetFloat(PREFS_PREFIX + "refreshInterval", 0.5f);
    }

    // -------------------------------------------------------------------------
    // Auto-refresh
    // -------------------------------------------------------------------------

    private void UpdateAutoRefresh()
    {
        if (isProcessing) return;
        if (!autoRefresh) return;
        if (EditorApplication.timeSinceStartup - lastRefreshTime < autoRefreshInterval) return;

        lastRefreshTime = EditorApplication.timeSinceStartup;
        RefreshSelectedSpriteSheets();
        Repaint();
    }

    // -------------------------------------------------------------------------
    // GUI
    // -------------------------------------------------------------------------

    private void OnGUI()
    {
        int indexToDiscard = -1;
        bool doSliceAll      = false;
        bool doAutoDetectAll = false;
        bool doApplyToChecked  = false;
        bool doDiscardChecked  = false;

        GUI.enabled = !isProcessing;

        DrawGlobalControls(ref doAutoDetectAll, ref doSliceAll, ref doApplyToChecked, ref doDiscardChecked);
        GUILayout.Space(SPACER);
        DrawJobList(ref indexToDiscard);

        GUI.enabled = true;

        // Defer all mutations to end of frame to avoid GUILayout layout errors
        if (indexToDiscard >= 0)
            activeJobs.RemoveAt(indexToDiscard);

        if (doApplyToChecked)
            ApplyGlobalsToChecked();

        if (doDiscardChecked)
            activeJobs.RemoveAll(j => j.isEnabled);

        if (doAutoDetectAll)
        {
            foreach (var job in activeJobs)
                if (job.isEnabled) AutoDetectGrid(job);
        }

        if (doSliceAll)
            SliceAllEnabled();
    }

    private void DrawGlobalControls(ref bool autoDetectAll, ref bool sliceAll, ref bool applyToChecked, ref bool discardChecked)
    {
        GUILayout.Label("Global Templates & Batch Actions", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        globalSliceMode = (SliceMode)EditorGUILayout.EnumPopup("Global Slice Mode", globalSliceMode);

        if (globalSliceMode == SliceMode.CellCount)
        {
            globalCellsPerRow    = Mathf.Max(1, EditorGUILayout.IntField("Global Cols",   globalCellsPerRow));
            globalCellsPerColumn = Mathf.Max(1, EditorGUILayout.IntField("Global Rows",   globalCellsPerColumn));
        }
        else
        {
            globalCellWidth  = Mathf.Max(1, EditorGUILayout.IntField("Global Cell Width",  globalCellWidth));
            globalCellHeight = Mathf.Max(1, EditorGUILayout.IntField("Global Cell Height", globalCellHeight));
        }

        globalPivot = (SpriteAlignment)EditorGUILayout.EnumPopup("Global Pivot", globalPivot);
        if (globalPivot == SpriteAlignment.Custom)
            globalCustomPivot = EditorGUILayout.Vector2Field("Global Custom Pivot", globalCustomPivot);

        GUILayout.Space(4);
        globalIgnoreEmpty  = EditorGUILayout.Toggle("Ignore Empty Sprites", globalIgnoreEmpty);
        globalNamePattern  = EditorGUILayout.TextField("Name Pattern", globalNamePattern);
        EditorGUILayout.HelpBox("{texture} {index} {row} {col} are supported tokens.", MessageType.None);

        GUILayout.Space(4);
        EditorGUILayout.LabelField("Offset & Padding (pixels)", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        globalOffsetX  = Mathf.Max(0, EditorGUILayout.IntField("Offset X",  globalOffsetX));
        globalOffsetY  = Mathf.Max(0, EditorGUILayout.IntField("Offset Y",  globalOffsetY));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        globalPaddingX = Mathf.Max(0, EditorGUILayout.IntField("Padding X", globalPaddingX));
        globalPaddingY = Mathf.Max(0, EditorGUILayout.IntField("Padding Y", globalPaddingY));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);
        autoRefresh = EditorGUILayout.Toggle("Auto Refresh", autoRefresh);
        if (autoRefresh)
            autoRefreshInterval = Mathf.Max(0.2f, EditorGUILayout.FloatField("Refresh Interval (s)", autoRefreshInterval));

        GUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Globals to Checked", GUILayout.Height(22))) applyToChecked  = true;
        if (GUILayout.Button("Discard Checked",          GUILayout.Height(22))) discardChecked  = true;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto-Detect Grid (All Checked)", GUILayout.Height(22))) autoDetectAll = true;
        if (GUILayout.Button("Slice All Checked",              GUILayout.Height(22))) sliceAll      = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawJobList(ref int indexToDiscard)
    {
        GUILayout.Label("Selected Sprite Sheets Queue", EditorStyles.boldLabel);
        if (activeJobs.Count == 0)
        {
            EditorGUILayout.HelpBox("Select a Sprite Sheet or Folder in the Project Window.", MessageType.Info);
            return;
        }

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // Forward iteration — order matches the queue label
        for (int i = 0; i < activeJobs.Count; i++)
        {
            SlicingJob job = activeJobs[i];
            EditorGUILayout.BeginVertical("box");

            // --- Header row ---
            EditorGUILayout.BeginHorizontal();
            job.isEnabled = EditorGUILayout.Toggle(job.isEnabled, GUILayout.Width(20));

            GUI.enabled = job.isEnabled && !isProcessing;
            GUILayout.Label(job.texture.name, EditorStyles.boldLabel, GUILayout.Width(150));
            GUILayout.Label($"{job.texture.width}x{job.texture.height}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Auto-Detect", GUILayout.Width(85), GUILayout.Height(18))) AutoDetectGrid(job);
            if (GUILayout.Button("Slice Only",  GUILayout.Width(75), GUILayout.Height(18))) SliceSingle(job);

            GUI.enabled = !isProcessing;
            if (GUILayout.Button("Discard", GUILayout.Width(65), GUILayout.Height(18))) indexToDiscard = i;
            EditorGUILayout.EndHorizontal();

            if (job.isEnabled)
            {
                GUI.enabled = !isProcessing;
                GUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();

                // Left column — slice mode
                EditorGUILayout.BeginVertical(GUILayout.Width(250));
                job.sliceMode = (SliceMode)EditorGUILayout.EnumPopup("Slice Mode", job.sliceMode);
                if (job.sliceMode == SliceMode.CellCount)
                {
                    job.cellsPerRow    = Mathf.Max(1, EditorGUILayout.IntField("Cols (Per Row)", job.cellsPerRow));
                    job.cellsPerColumn = Mathf.Max(1, EditorGUILayout.IntField("Rows (Per Col)", job.cellsPerColumn));
                }
                else
                {
                    job.cellWidth  = Mathf.Max(1, EditorGUILayout.IntField("Width (px)",  job.cellWidth));
                    job.cellHeight = Mathf.Max(1, EditorGUILayout.IntField("Height (px)", job.cellHeight));
                }
                EditorGUILayout.EndVertical();

                GUILayout.Space(20);

                // Right column — pivot, naming, padding
                EditorGUILayout.BeginVertical();
                job.pivot = (SpriteAlignment)EditorGUILayout.EnumPopup("Pivot Preset", job.pivot);
                if (job.pivot == SpriteAlignment.Custom)
                    job.customPivot = EditorGUILayout.Vector2Field("Custom Pivot", job.customPivot);

                job.ignoreEmpty  = EditorGUILayout.Toggle("Ignore Empty", job.ignoreEmpty);
                job.namePattern  = EditorGUILayout.TextField("Name Pattern", job.namePattern);

                EditorGUILayout.BeginHorizontal();
                job.offsetX  = Mathf.Max(0, EditorGUILayout.IntField("Offset X",  job.offsetX));
                job.offsetY  = Mathf.Max(0, EditorGUILayout.IntField("Offset Y",  job.offsetY));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                job.paddingX = Mathf.Max(0, EditorGUILayout.IntField("Padding X", job.paddingX));
                job.paddingY = Mathf.Max(0, EditorGUILayout.IntField("Padding Y", job.paddingY));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();

                if (job.autoDetected)
                {
                    GUILayout.Space(3);
                    EditorGUILayout.HelpBox($"Grid auto-detected: {job.cellWidth}x{job.cellHeight} px.", MessageType.None);
                }

                GUI.enabled = true;
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        GUILayout.EndScrollView();
    }

    // -------------------------------------------------------------------------
    // Selection refresh
    // -------------------------------------------------------------------------

    private void RefreshSelectedSpriteSheets()
    {
        if (isProcessing) return;

        // Use AssetDatabase.FindAssets — faster than Directory.GetFiles
        Object[] selectedAssets = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);

        // Build an order-independent set for comparison
        HashSet<string> newPaths = new HashSet<string>();
        foreach (Object obj in selectedAssets)
            newPaths.Add(AssetDatabase.GetAssetPath(obj));

        if (newPaths.SetEquals(cachedSelectionPaths) && activeJobs.Count > 0)
            return;

        cachedSelectionPaths = newPaths;

        // Collect textures using Unity's asset database query
        List<Texture2D> selectedTextures = new List<Texture2D>();

        foreach (Object obj in selectedAssets)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);

            if (Directory.Exists(assetPath))
            {
                // FindAssets is faster and Unity-native; no filesystem scan
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { assetPath });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null && IsValidSpriteTexture(tex) && !selectedTextures.Contains(tex))
                        selectedTextures.Add(tex);
                }
            }
            else
            {
                Texture2D tex = obj as Texture2D;
                if (tex != null && IsValidSpriteTexture(tex) && !selectedTextures.Contains(tex))
                    selectedTextures.Add(tex);
            }
        }

        // Preserve existing job settings for textures already in the queue
        List<SlicingJob> newJobsList = new List<SlicingJob>();
        foreach (Texture2D tex in selectedTextures)
        {
            SlicingJob existing = activeJobs.Find(j => j.texture == tex);
            newJobsList.Add(existing ?? CreateJobFromGlobals(tex));
        }
        activeJobs = newJobsList;
    }

    private SlicingJob CreateJobFromGlobals(Texture2D tex) => new SlicingJob
    {
        texture       = tex,
        sliceMode     = globalSliceMode,
        cellsPerRow   = globalCellsPerRow,
        cellsPerColumn = globalCellsPerColumn,
        cellWidth     = globalCellWidth,
        cellHeight    = globalCellHeight,
        pivot         = globalPivot,
        customPivot   = globalCustomPivot,
        ignoreEmpty   = globalIgnoreEmpty,
        namePattern   = globalNamePattern,
        paddingX      = globalPaddingX,
        paddingY      = globalPaddingY,
        offsetX       = globalOffsetX,
        offsetY       = globalOffsetY,
    };

    private void ApplyGlobalsToChecked()
    {
        foreach (var job in activeJobs)
        {
            if (!job.isEnabled) continue;
            job.sliceMode      = globalSliceMode;
            job.cellsPerRow    = globalCellsPerRow;
            job.cellsPerColumn = globalCellsPerColumn;
            job.cellWidth      = globalCellWidth;
            job.cellHeight     = globalCellHeight;
            job.pivot          = globalPivot;
            job.customPivot    = globalCustomPivot;
            job.ignoreEmpty    = globalIgnoreEmpty;
            job.namePattern    = globalNamePattern;
            job.paddingX       = globalPaddingX;
            job.paddingY       = globalPaddingY;
            job.offsetX        = globalOffsetX;
            job.offsetY        = globalOffsetY;
        }
    }

    private bool IsValidSpriteTexture(Texture2D texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        return AssetImporter.GetAtPath(path) is TextureImporter;
    }

    // -------------------------------------------------------------------------
    // Auto-detect grid
    // -------------------------------------------------------------------------

    private void AutoDetectGrid(SlicingJob job)
    {
        if (job?.texture == null) return;
        string assetPath = AssetDatabase.GetAssetPath(job.texture);
        if (string.IsNullOrEmpty(assetPath)) return;

        Texture2D tempTex = new Texture2D(2, 2);
        try
        {
            byte[] fileData = File.ReadAllBytes(assetPath);
            if (!tempTex.LoadImage(fileData))
            {
                Debug.LogWarning($"[SliceSpriteSheets] Could not load image data for {job.texture.name}.");
                return;
            }

            int texWidth  = tempTex.width;
            int texHeight = tempTex.height;

            // Bulk pixel fetch — far faster than per-pixel GetPixel
            Color[] allPixels = tempTex.GetPixels();

            bool[] colHasPixels = new bool[texWidth];
            bool[] rowHasPixels = new bool[texHeight];

            for (int y = 0; y < texHeight; y++)
            {
                for (int x = 0; x < texWidth; x++)
                {
                    if (allPixels[y * texWidth + x].a > 0.01f)
                    {
                        colHasPixels[x] = true;
                        rowHasPixels[y] = true;
                    }
                }
            }

            int detectedWidth  = FindBestGridSize(colHasPixels, texWidth);
            int detectedHeight = FindBestGridSize(rowHasPixels, texHeight);

            // Only force square when the texture itself is square AND the score supports it
            if (detectedWidth != detectedHeight && texWidth == texHeight)
            {
                float scoreAsSquare = GetGridFitScore(rowHasPixels, texHeight, detectedWidth);
                if (scoreAsSquare > 0.85f)
                    detectedHeight = detectedWidth;
            }

            // Sanity check — detected cell must be smaller than the texture
            if (detectedWidth >= texWidth || detectedHeight >= texHeight)
            {
                Debug.LogWarning($"[SliceSpriteSheets] Auto-detect produced an implausible cell size for {job.texture.name}. Skipping.");
                return;
            }

            job.sliceMode    = SliceMode.CellSize;
            job.cellWidth    = detectedWidth;
            job.cellHeight   = detectedHeight;
            job.autoDetected = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SliceSpriteSheets] Grid detection failed on {job.texture.name}: {ex.Message}");
        }
        finally
        {
            DestroyImmediate(tempTex); // always runs, even on exception
        }
    }

    private int FindBestGridSize(bool[] pixelPresence, int totalSize)
    {
        // Broader candidate list covering common mobile/UI sizes
        HashSet<int> candidateSet = new HashSet<int> { 16, 24, 32, 40, 48, 64, 72, 80, 96, 128, 160, 256, 320 };

        // Add all divisors of totalSize (multiples of 8)
        for (int i = 8; i <= totalSize / 2; i += 8)
            if (totalSize % i == 0) candidateSet.Add(i);

        List<int> candidates = new List<int>(candidateSet);
        candidates.Sort();

        int   bestCandidate = 32;
        float bestScore     = -1f;

        foreach (int candidate in candidates)
        {
            float score = GetGridFitScore(pixelPresence, totalSize, candidate);
            if (score > bestScore)
            {
                bestScore     = score;
                bestCandidate = candidate;
            }
        }
        return bestCandidate;
    }

    private float GetGridFitScore(bool[] pixelPresence, int totalSize, int candidate)
    {
        if (candidate <= 0 || candidate >= totalSize) return 0f;

        int gridLines = 0, cleanCuts = 0;
        for (int pos = candidate; pos < totalSize; pos += candidate)
        {
            gridLines++;
            bool isClean = true;
            for (int offset = -1; offset <= 1; offset++)
            {
                int checkPos = pos + offset;
                if (checkPos >= 0 && checkPos < totalSize && pixelPresence[checkPos])
                {
                    isClean = false;
                    break;
                }
            }
            if (isClean) cleanCuts++;
        }
        return gridLines == 0 ? 0f : (float)cleanCuts / gridLines;
    }

    // -------------------------------------------------------------------------
    // Slice execution
    // -------------------------------------------------------------------------

    private void SliceAllEnabled()
    {
        isProcessing = true;
        try
        {
            var enabledJobs = activeJobs.Where(j => j.isEnabled).ToList();
            for (int i = 0; i < enabledJobs.Count; i++)
            {
                EditorUtility.DisplayProgressBar(
                    "Batch Slicing",
                    $"Processing {enabledJobs[i].texture.name}... ({i + 1}/{enabledJobs.Count})",
                    (float)(i + 1) / enabledJobs.Count);

                ExecuteSlice(enabledJobs[i]);
            }
        }
        finally
        {
            isProcessing = false;
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            RefreshSelectedSpriteSheets();
        }
    }

    private void SliceSingle(SlicingJob job)
    {
        isProcessing = true;
        try
        {
            EditorUtility.DisplayProgressBar("Slicing Sprite", $"Processing {job.texture.name}...", 0.5f);
            ExecuteSlice(job);
        }
        finally
        {
            isProcessing = false;
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            RefreshSelectedSpriteSheets();
        }
    }

    private void ExecuteSlice(SlicingJob job)
    {
        string assetPath = AssetDatabase.GetAssetPath(job.texture);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        // Register undo before modifying the importer
        Undo.RegisterCompleteObjectUndo(importer, $"Slice {job.texture.name}");

        if (importer.textureType != TextureImporterType.Sprite)
            importer.textureType = TextureImporterType.Sprite;
        if (importer.spriteImportMode != SpriteImportMode.Multiple)
            importer.spriteImportMode = SpriteImportMode.Multiple;

        // Resolve cell dimensions
        int cellW, cellH;
        if (job.sliceMode == SliceMode.CellCount)
        {
            cellW = (job.texture.width  - job.offsetX) / Mathf.Max(1, job.cellsPerRow);
            cellH = (job.texture.height - job.offsetY) / Mathf.Max(1, job.cellsPerColumn);
        }
        else
        {
            cellW = job.cellWidth;
            cellH = job.cellHeight;
        }

        // Guard against zero/negative cell sizes
        if (cellW <= 0 || cellH <= 0)
        {
            Debug.LogError($"[SliceSpriteSheets] Invalid cell size ({cellW}x{cellH}) for {job.texture.name}. Skipping.");
            return;
        }

        int cols = (job.texture.width  - job.offsetX) / (cellW + job.paddingX);
        int rows = (job.texture.height - job.offsetY) / (cellH + job.paddingY);

        Texture2D tempTex = new Texture2D(2, 2);
        try
        {
            byte[] fileData = File.ReadAllBytes(assetPath);
            tempTex.LoadImage(fileData);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            // Build a lookup of existing sprite rects by name so we can preserve GUIDs
            var existingRects = dataProvider.GetSpriteRects();
            Dictionary<string, GUID> existingGuidByName = new Dictionary<string, GUID>();
            foreach (var sr in existingRects)
                existingGuidByName[sr.name] = sr.spriteID;

            List<SpriteRect> spriteRects = GenerateSpriteRects(
                tempTex, rows, cols, cellH, cellW, job, assetPath, existingGuidByName);

            dataProvider.SetSpriteRects(spriteRects.ToArray());
            dataProvider.Apply();
        }
        finally
        {
            DestroyImmediate(tempTex);
        }

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    // -------------------------------------------------------------------------
    // Sprite rect generation
    // -------------------------------------------------------------------------

    private List<SpriteRect> GenerateSpriteRects(
        Texture2D texture,
        int rows, int columns,
        int cellH, int cellW,
        SlicingJob job,
        string assetPath,
        Dictionary<string, GUID> existingGuidByName)
    {
        string baseName = Path.GetFileNameWithoutExtension(assetPath);
        var spriteRects = new List<SpriteRect>();
        int index = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                int x = job.offsetX + col * (cellW + job.paddingX);
                int y = job.offsetY + (rows - 1 - row) * (cellH + job.paddingY); // bottom-up

                // Skip out-of-bounds cells
                if (x + cellW > texture.width || y + cellH > texture.height) continue;

                if (job.ignoreEmpty && IsCellEmpty(texture, x, y, cellW, cellH))
                {
                    index++;
                    continue;
                }

                string spriteName = ResolveSpriteName(job.namePattern, baseName, index, row, col);

                // Reuse existing GUID if this name was already sliced — prevents broken references
                GUID spriteID = existingGuidByName.TryGetValue(spriteName, out GUID existingID)
                    ? existingID
                    : GUID.Generate();

                spriteRects.Add(new SpriteRect
                {
                    rect      = new Rect(x, y, cellW, cellH),
                    name      = spriteName,
                    alignment = job.pivot,
                    pivot     = job.pivot == SpriteAlignment.Custom
                                    ? job.customPivot
                                    : PivotMap.TryGetValue(job.pivot, out Vector2 p) ? p : new Vector2(0.5f, 0.5f),
                    border    = Vector4.zero,
                    spriteID  = spriteID,
                });

                index++;
            }
        }
        return spriteRects;
    }

    private static string ResolveSpriteName(string pattern, string textureName, int index, int row, int col)
    {
        return pattern
            .Replace("{texture}", textureName)
            .Replace("{index}",   index.ToString())
            .Replace("{row}",     row.ToString())
            .Replace("{col}",     col.ToString());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true when every pixel in the cell region is fully transparent.
    /// Returns false (treat as non-empty) when the region is out of bounds.
    /// </summary>
    private static bool IsCellEmpty(Texture2D texture, int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || x + width > texture.width || y + height > texture.height)
            return false; // out of bounds → conservatively non-empty

        Color[] pixels = texture.GetPixels(x, y, width, height);
        foreach (Color pixel in pixels)
            if (pixel.a > 0.01f) return false;

        return true;
    }
}
#endif
