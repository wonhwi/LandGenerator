using Cysharp.Threading.Tasks;
using DigitalOpus.MB.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#region Enum, Class 

[System.Flags]
public enum Option
{
  None = 0,
  NavMeshBake = 1 << 1,
  MeshBake = 1 << 2,
}

[System.Flags]
public enum CityType
{
  None = 0,
  SeoulA = 1 << 1,
  Newyork = 1 << 2,
  London = 1 << 3,
  Tokyo = 1 << 4,
  Berlin = 1 << 5,
  SaoPaulo = 1 << 6,
  Hongkong = 1 << 7,
  Amsterdam = 1 << 8,

}

public enum MeshBakeType
{
  All = 0,
  SharedMaterial = 1
}


public class LandGeneratorData
{
  public string Land_Code;

  public long index;
  public int State;
  public int ParcelCount;

  public short CoordiStartX;
  public short CoordiEndX;
  public short CoordiStartY;
  public short CoordiEndY;

  public string land_model;
  public int land_rotation;

}

#endregion

#region Editor

[CustomEditor(typeof(SceneGenerateTool))]
public class SceneGenerateToolEditor : Editor
{
  private SceneGenerateTool sceneGenerateTool;
  private SerializedObject m_target;

  GUILayoutOption[] GUILayoutOptionList = new GUILayoutOption[]
    {
      GUILayout.MinWidth(100),
      GUILayout.MaxWidth(200),
      GUILayout.Height(50),
      GUILayout.ExpandWidth(true),
      
    };

  private void OnEnable()
  {
    sceneGenerateTool = (SceneGenerateTool)target;
    m_target = new SerializedObject(sceneGenerateTool);
  }

  public override void OnInspectorGUI()
  {
    EditorGUILayout.BeginVertical(GUI.skin.box);
    EditorGUILayout.LabelField($"���� ���� ���� ������", EditorStyles.boldLabel);
    EditorGUILayout.Space(10, true);
    sceneGenerateTool.dataMap = EditorGUILayout.ObjectField("DataMap", sceneGenerateTool.dataMap, typeof(Data), false) as Data;
    EditorGUILayout.Space(10);
    sceneGenerateTool.landDataPath = EditorGUILayout.TextField("���� ������ ���� ��ġ", sceneGenerateTool.landDataPath);
    EditorGUILayout.EndVertical();
    EditorGUILayout.Space(10, true);
    EditorGUILayout.BeginVertical(GUI.skin.box);
    EditorGUILayout.LabelField($"���� ���� ���� ���� �ɼ�", EditorStyles.boldLabel);
    EditorGUILayout.Space(10, true);
    sceneGenerateTool.cityType = (CityType)EditorGUILayout.EnumFlagsField("������ MetaCity���� �������ּ��� (���� ���� ����)", sceneGenerateTool.cityType);
    
    sceneGenerateTool.option = (Option)EditorGUILayout.EnumFlagsField("�� ���� �Ϸ� �� �߰� ��� ��� ����", sceneGenerateTool.option);

    if (!sceneGenerateTool.option.Equals(Option.None))
    {
      EditorGUILayout.Space(10);
      EditorGUILayout.LabelField($"���� ���� ������ ���� �ɼ�", EditorStyles.boldLabel);
    }
    
    if (sceneGenerateTool.option.HasFlag(Option.NavMeshBake))
    {
      EditorGUILayout.LabelField($"���� ���� ���", EditorStyles.boldLabel);
    }

    if (sceneGenerateTool.option.HasFlag(Option.MeshBake))
    {
      EditorGUILayout.HelpBox($"All ���� �� Mesh�� ����\nSharedMaterial = ���� ���� ���� ����", MessageType.Info);

      sceneGenerateTool.meshBakeType = (MeshBakeType)EditorGUILayout.EnumPopup("MeshBakeType", sceneGenerateTool.meshBakeType);
    }
    else
    {
      sceneGenerateTool.meshBakeType = default;
    }

    EditorGUILayout.EndVertical();
    EditorGUILayout.Space(10);

    if (GUILayout.Button("���� ����", GUILayoutOptionList))
    {
      
      sceneGenerateTool.StartGenerateScene().Forget();
    }

  }
}

#endregion

public class SceneGenerateTool : MonoBehaviour
{
  

  private Dictionary<CityType, List<LandGeneratorData>> LandDictionary = new Dictionary<CityType, List<LandGeneratorData>>();

  private Dictionary<string, GameObject> PrefabPathDictionary = new Dictionary<string, GameObject>();


  /// <summary>
  /// ScriptableObject Data
  /// </summary>
  public Data dataMap;

  /// <summary>
  /// �� ���� �� �� Ÿ�Ե�
  /// </summary>
  public CityType cityType;

  /// <summary>
  /// �� ���� �Ϸ� �� ��ó�� ��� ��� ����
  /// </summary>
  public Option option;

  public MeshBakeType meshBakeType;

  /// <summary>
  /// ���� ������ ������ �����ϴ� ��ġ
  /// </summary>
  public string landDataPath = "";

  /// <summary>
  /// ������ ���尡 ��ġ�Ǵ� ���� ���
  /// </summary>
  public readonly string scenePath = "Assets/0_Scenes/";

  /// <summary>
  /// ������ ��ġ ����
  /// </summary>
  public readonly string prefabPath = "Assets/2_Prefabs/";


  public async UniTask StartGenerateScene()
  {
    await this.LoadPrefabData();

    await this.LoadData();

    await this.GenerateScene();
    
    //StaticOcclusionCulling.GenerateInBackground();    //��ŧ���� �ø�
    //Lightmapping.Bake();                              //����Ʈ�� Bake
  }

  /// <summary>
  /// ������ ������ ����
  /// </summary>
  /// <returns></returns>
  public async UniTask LoadPrefabData()
  {
    //������ ���� ������ ����
    PrefabPathDictionary.Clear();

    string[] prefabFileDirectory = Directory.GetFiles(prefabPath, "*.prefab", SearchOption.AllDirectories).Select(n => n.Replace(@"\", "/")).ToArray();

    foreach (var prefabFile in prefabFileDirectory)
    {
      string[] prefabSplitName = prefabFile.Split('/');

      string prefabName = prefabSplitName[prefabSplitName.Length - 1].Replace(".prefab", "");

      //Debug.Whi($"{prefabName} : path : {prefabFile}");

      GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFile);
      
      PrefabPathDictionary.Add(prefabName, prefabObject);
    }
    
  }

  /// <summary>
  /// Path ��� ������ �ε�
  /// </summary>
  public async UniTask LoadData()
  {

    if (cityType.Equals(CityType.None))
    {
      Debug.LogError("������ City���� ������ �ּ���");
      return;
    }

    if (!Directory.Exists(landDataPath))
    {
      Debug.LogError("�ùٸ��� ���� ����Դϴ�.");
      return;
    }

    LandDictionary.Clear();

    foreach (CityType value in Enum.GetValues(typeof(CityType)))
    {
      if (value.Equals(CityType.None))
        continue;

      if (cityType.HasFlag(value))
      {
        string dataPath = $"{landDataPath}" + @"\" + $"{value}.json";

        if (File.Exists(dataPath)) //���� ������ ���
        {
          string str = File.ReadAllText(dataPath);
          if (!string.IsNullOrEmpty(str))
          {
            var json = JArray.Parse(str);

            var landData = json.ToObject<List<LandGeneratorData>>();

            LandDictionary.Add(value, landData.Where(n => n.Land_Code.Equals(value.ToString())).ToList());
          }
          else
          {
            Debug.LogError($"{dataPath} ������ ��� �ֽ��ϴ�.");
          }
        }
        else
        {
          Debug.LogError($"{value} ������ ������ �������� �ʽ��ϴ�.");
        }

      }
    }

    
  }

  /// <summary>
  /// ���� ������Ʈ ����
  /// </summary>
  public async UniTask GenerateScene()
  {
    Debug.LogError("GenerateScene");

    for (int i = 0; i < LandDictionary.Keys.Count; i++)
    {
      CityType cityType = LandDictionary.Keys.ElementAt(i);

      Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

      string sceneName = $"{this.scenePath}Scene_Land_{cityType}.unity";

      Debug.Log(sceneName + "���� ��");

      //������ �����ϸ� ����
      if (File.Exists(sceneName))
        AssetDatabase.DeleteAsset(sceneName);

      GameObject bundleMap = new("Map");

      for (int j = 0; j < LandDictionary[cityType].Count; j++)
      {
        LandGeneratorData landData = LandDictionary[cityType][j];

        if (string.IsNullOrEmpty(landData.land_model))
          continue;

        if (!PrefabPathDictionary.ContainsKey(landData.land_model))
          continue;

        EditorUtility.DisplayProgressBar(
          $"���带 �������Դϴ� {i + 1}/{LandDictionary.Count}",
          $"������ {cityType} ������� : {j + 1}/{LandDictionary[cityType].Count}",
          (float)j / (float)LandDictionary[cityType].Count);

        GameObject createdObject = PrefabUtility.InstantiatePrefab(PrefabPathDictionary[landData.land_model], bundleMap.transform) as GameObject;

        //Debug.LogError($"{createdObject != null} \n {JsonConvert.SerializeObject(landData)}");

        //await UniTask.WaitUntil(() => createdObject != null);
        
        createdObject.transform.SetPositionAndRotation(
          CalculationCoordinate(landData), 
          Quaternion.Euler(0f, (landData.land_rotation - 1) * -90, 0f)
          );

      }

      this.SetCamera(cityType);

      if (this.option.HasFlag(Option.MeshBake))
      {
        EditorUtility.ClearProgressBar();

        EditorUtility.DisplayProgressBar(
          $"���带 �������Դϴ� {i + 1}/{LandDictionary.Count}",
          $"������ �� �������...",
          1f
        );

        this.SetMeshBake(bundleMap.transform);
      }

      EditorSceneManager.SaveScene(newScene, sceneName);

      EditorUtility.ClearProgressBar();

      
    }

    EditorUtility.DisplayDialog("�۾� �Ϸ�", "���� ������ �Ϸ�Ǿ����ϴ�.", "Ȯ��");

  }
  
  private void SetCamera(CityType cityType)
  {
    Camera cam = Camera.main;

    if (cam)
    {
      MetaCityData metaCityData = this.dataMap.GetMetaCityData(cityType);

      cam.transform.SetPositionAndRotation(metaCityData.cameraData.position, Quaternion.Euler(metaCityData.cameraData.rotation));

      cam.farClipPlane = 10000f;

    }
  }

  private void SetMeshBake(Transform bundleMap)
  {
    MeshBakerTool.MeshBaker(this.dataMap.meshBaker, bundleMap, this.meshBakeType);

  }


  /// <summary>
  /// Coordinate������ ���� Position ����ϱ�
  /// </summary>
  public Vector3 CalculationCoordinate(LandGeneratorData data)
  {
    short startPosX = data.CoordiStartX;
    short endPosX = data.CoordiEndX;
    short startPosY = data.CoordiStartY;
    short endPosY = data.CoordiEndY;

    float coord_x = (startPosX + endPosX) * 0.5f;
    float coord_y = (startPosY + endPosY) * 0.5f;

    return new Vector3(coord_x, 0f, coord_y) * 24;
  }
}
