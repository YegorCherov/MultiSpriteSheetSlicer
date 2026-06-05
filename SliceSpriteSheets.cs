using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System;
#if UNITY_EDITOR
public class SliceSpriteSheets : EditorWindow
{
    private const float SPACER = 10.0f;
    public enum SliceMode
    {
        CellCount,
        CellSize
    }

    public enum PivotUnitMode
    {
        Normalized,
        Pixels
    }

    private SliceMode sliceMode = SliceMode.CellCount;
    private int RowCount = 4;
    private int ColCount = 4;
    private int cellWidth = 32;
    private int cellHeight = 32;

    private Vector2 pivotPosition = new Vector2(0.5f, 0.5f);
    private PivotUnitMode pivotUnitMode = PivotUnitMode.Normalized;

    private bool LockSheets = false;
    private const float REFRESHTIME = 0.2f;
    private double lastRefreshTime = 0f;

    private Vector2 scrollPosition;
    private List<Texture2D> selectedSpriteSheets = new List<Texture2D>();

    //Allignments defined by unity
    private SpriteAlignment SelectedPivot = SpriteAlignment.Center;

    [MenuItem("Tools/Slice Sprite Sheets")]
    public static void ShowWindow()
    {
        GetWindow<SliceSpriteSheets>("Slice Sprite Sheets");
    }

    private void OnEnable()
    {
        Selection.selectionChanged += Repaint;
        RefreshSelectedSpriteSheets();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= Repaint;
    }
    private void OnDestroy() 
    {
        Selection.selectionChanged -= Repaint;
    }

    private void OnGUI()
    {
        DrawSliceMode();

        GUILayout.Space(SPACER);

        DrawPivot();

        GUILayout.Space(SPACER);

        LockSheets = EditorGUILayout.Toggle("Lock Sheets", LockSheets);

        if (GUILayout.Button("Slice Selected Sprite Sheets"))
            SliceSelectedSpriteSheets();

        GUILayout.Space(SPACER);

        DrawSpriteSheets();
    }

    private void Update()
    {
        if(LockSheets)
            return;
        if(EditorApplication.timeSinceStartup - lastRefreshTime < REFRESHTIME)
            return;
        lastRefreshTime = EditorApplication.timeSinceStartup;
        RefreshSelectedSpriteSheets();
        Repaint();
    }

    private void DrawSliceMode()
    {
        GUILayout.Label("Slicing Options", EditorStyles.boldLabel);

        sliceMode = (SliceMode)EditorGUILayout.EnumPopup("Slice Mode", sliceMode);

        if (sliceMode == SliceMode.CellCount)
        {
            ColCount = EditorGUILayout.IntField("Cells Per Row", ColCount);
            RowCount = EditorGUILayout.IntField("Cells Per Column", RowCount);
            return;
        }
        cellWidth = EditorGUILayout.IntField("Cell Width", cellWidth);
        cellHeight = EditorGUILayout.IntField("Cell Height", cellHeight);
    }


    private void DrawPivot()
    {
        GUILayout.Label("Pivot Options", EditorStyles.boldLabel);
        SelectedPivot = (SpriteAlignment)EditorGUILayout.EnumPopup("Pivot Preset", SelectedPivot);

        if(SelectedPivot == SpriteAlignment.Custom)
        {
            pivotUnitMode = (PivotUnitMode)EditorGUILayout.EnumPopup("Pivot Unit Mode", pivotUnitMode);
            pivotPosition = EditorGUILayout.Vector2Field("Custom Pivot", pivotPosition);
            return;
        }

        pivotPosition = AlignToPivot(SelectedPivot);
    }
    private static Vector2 AlignToPivot(SpriteAlignment alignment)
    {
        //bottom -> 0, center -> 0.5, top -> 1
        float height = 1.0f - (int)alignment / 3 * 0.5f;
        //left -> 0, center -> 0.5, right -> 1
        float width = (int)alignment % 3 * 0.5f;
        return new(width,height);
    }
    private void DrawSpriteSheets()
    {
        GUILayout.Label("Selected Sprite Sheets", EditorStyles.boldLabel);

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        foreach (Texture2D spriteSheet in selectedSpriteSheets)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(spriteSheet.name, GUILayout.Width(200));
            GUILayout.Label(string.Format("{0}x{1}", spriteSheet.width, spriteSheet.height));
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }
    private void RefreshSelectedSpriteSheets()
    {
        selectedSpriteSheets.Clear();

        UnityEngine.Object[] selectedAssets = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);
        foreach (UnityEngine.Object obj in selectedAssets)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            Texture2D spriteSheet;
            // If the selected asset is a folder, get all sprite sheets inside it
            if (Directory.Exists(assetPath))
            {
                string[] spriteSheetPaths = Directory.GetFiles(assetPath, "*.png", SearchOption.AllDirectories);
                foreach (string path in spriteSheetPaths)
                {
                    spriteSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (spriteSheet != null && IsValidSpriteSheet(spriteSheet))
                        selectedSpriteSheets.Add(spriteSheet);
                }
                continue;
            }
            // If the selected asset is a sprite sheet, add it to the list
            spriteSheet = obj as Texture2D;
            if (spriteSheet != null && IsValidSpriteSheet(spriteSheet))
                selectedSpriteSheets.Add(spriteSheet);
        }
    }
    private bool IsValidSpriteSheet(Texture2D spriteSheet)
    {
        string assetPath = AssetDatabase.GetAssetPath(spriteSheet);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        return importer != null && importer.textureType == TextureImporterType.Sprite;
    }
    private void SliceSelectedSpriteSheets()
    {
        Action<Texture2D> sliceFunc = sliceMode switch
        {
            SliceMode.CellCount => (spritesheet) => SliceByCell(spritesheet, RowCount, ColCount, SelectedPivot, pivotPosition),
            SliceMode.CellSize => (spritesheet) => SliceBySize(spritesheet, cellHeight, cellWidth, SelectedPivot, pivotPosition),
            _ => null
        };
        if(sliceFunc == null)
        {
            Debug.LogError($"Undefined slice mode given {sliceMode}");
            return;
        }

        foreach (Texture2D spritesheet in selectedSpriteSheets)
        {
            sliceFunc(spritesheet);
        }

        AssetDatabase.Refresh();
        RefreshSelectedSpriteSheets();
    }
    public static void SliceByCell(Texture2D spriteSheet,int rows, int collums, SpriteAlignment? alignment = null, Vector2? pivot = null){
        alignment ??= SpriteAlignment.Center;
        pivot ??= new(0.5f,0.5f);
        string assetPath = AssetDatabase.GetAssetPath(spriteSheet);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if(importer == null)
            return;

        int Height = spriteSheet.height / rows;
        int Width = spriteSheet.width / collums;

        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritesheet = SpriteSlicer(spriteSheet,rows,collums,Height,Width,assetPath,alignment,pivot).ToArray();
        importer.spriteImportMode = SpriteImportMode.Multiple;

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    public static void SliceBySize(Texture2D spriteSheet, int height, int width, SpriteAlignment? alignment = null, Vector2? pivot = null)
    {
        alignment ??= SpriteAlignment.Center;
        pivot ??= new(0.5f,0.5f);
        string assetPath = AssetDatabase.GetAssetPath(spriteSheet);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if(importer == null)
            return;

        int rows = spriteSheet.height/height;
        int collums = spriteSheet.width / width;

        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritesheet = SpriteSlicer(spriteSheet,rows,collums,height,width,assetPath,alignment,pivot).ToArray();
        importer.spriteImportMode = SpriteImportMode.Multiple;

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }
    private static List<SpriteMetaData> SpriteSlicer(Texture2D spriteSheet, int rows, int collums, int height, int width, string assetPath = null,SpriteAlignment? alignment = null, Vector2? pivot = null)
    {
        alignment ??= SpriteAlignment.Center;
        pivot ??= new(0.5f,0.5f);
        assetPath ??= AssetDatabase.GetAssetPath(spriteSheet);
        string BaseName = Path.GetFileNameWithoutExtension(assetPath);
        List<SpriteMetaData> spriteData = new();
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < collums; j++)
            {
                SpriteMetaData smd = new()
                {
                    rect = new Rect(j * width, i * height, width, height),
                    name = string.Format("{0}_{1}", BaseName, (i * collums) + j),
                    alignment = (int)alignment.Value
                };
                if (alignment.Value == SpriteAlignment.Custom)
                    smd.pivot = pivot.Value; 

                smd.border = new(0, 0, 0, 0);

                spriteData.Add(smd);
            }
        }
        return spriteData;
    }
}
#endif