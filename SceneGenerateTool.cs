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
    EditorGUILayout.LabelField($"랜드 구성 관련 데이터", EditorStyles.boldLabel);
    EditorGUILayout.Space(10, true);
    sceneGenerateTool.dataMap = EditorGUILayout.ObjectField("DataMap", sceneGenerateTool.dataMap, typeof(Data), false) as Data;
    EditorGUILayout.Space(10);
    sceneGenerateTool.landDataPath = EditorGUILayout.TextField("랜드 데이터 파일 위치", sceneGenerateTool.landDataPath);
    EditorGUILayout.EndVertical();
    EditorGUILayout.Space(10, true);
    EditorGUILayout.BeginVertical(GUI.skin.box);
    EditorGUILayout.LabelField($"랜드 구성 관련 설정 옵션", EditorStyles.boldLabel);
    EditorGUILayout.Space(10, true);
    sceneGenerateTool.cityType = (CityType)EditorGUILayout.EnumFlagsField("생성할 MetaCity들을 선택해주세요 (다중 선택 가능)", sceneGenerateTool.cityType);
    
    sceneGenerateTool.option = (Option)EditorGUILayout.EnumFlagsField("씬 구성 완료 후 추가 사용 기능 설정", sceneGenerateTool.option);

    if (!sceneGenerateTool.option.Equals(Option.None))
    {
      EditorGUILayout.Space(10);
      EditorGUILayout.LabelField($"랜드 구성 디테일 설정 옵션", EditorStyles.boldLabel);
    }
    
    if (sceneGenerateTool.option.HasFlag(Option.NavMeshBake))
    {
      EditorGUILayout.LabelField($"개발 예정 기능", EditorStyles.boldLabel);
    }

    if (sceneGenerateTool.option.HasFlag(Option.MeshBake))
    {
      EditorGUILayout.HelpBox($"All 전부 한 Mesh로 묶기\nSharedMaterial = 같은 재질 끼리 묶기", MessageType.Info);

      sceneGenerateTool.meshBakeType = (MeshBakeType)EditorGUILayout.EnumPopup("MeshBakeType", sceneGenerateTool.meshBakeType);
    }
    else
    {
      sceneGenerateTool.meshBakeType = default;
    }

    EditorGUILayout.EndVertical();
    EditorGUILayout.Space(10);

    if (GUILayout.Button("랜드 구성", GUILayoutOptionList))
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
  /// 씬 구성 할 씬 타입들
  /// </summary>
  public CityType cityType;

  /// <summary>
  /// 씬 구성 완료 후 후처리 기능 사용 여부
  /// </summary>
  public Option option;

  public MeshBakeType meshBakeType;

  /// <summary>
  /// 랜드 데이터 파일이 존재하는 위치
  /// </summary>
  public string landDataPath = "";

  /// <summary>
  /// 구성한 랜드가 설치되는 파일 경로
  /// </summary>
  public readonly string scenePath = "Assets/0_Scenes/";

  /// <summary>
  /// 프리팹 위치 정보
  /// </summary>
  public readonly string prefabPath = "Assets/2_Prefabs/";


  public async UniTask StartGenerateScene()
  {
    await this.LoadPrefabData();

    await this.LoadData();

    await this.GenerateScene();
    
    //StaticOcclusionCulling.GenerateInBackground();    //오큘루전 컬링
    //Lightmapping.Bake();                              //라이트맵 Bake
  }

  /// <summary>
  /// 프리팹 데이터 저장
  /// </summary>
  /// <returns></returns>
  public async UniTask LoadPrefabData()
  {
    //프리팹 관련 정보들 저장
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
  /// Path 기반 데이터 로드
  /// </summary>
  public async UniTask LoadData()
  {

    if (cityType.Equals(CityType.None))
    {
      Debug.LogError("생성할 City들을 선택해 주세요");
      return;
    }

    if (!Directory.Exists(landDataPath))
    {
      Debug.LogError("올바르지 않은 경로입니다.");
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

        if (File.Exists(dataPath)) //정상 파일일 경우
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
            Debug.LogError($"{dataPath} 파일이 비어 있습니다.");
          }
        }
        else
        {
          Debug.LogError($"{value} 도시의 파일이 존재하지 않습니다.");
        }

      }
    }

    
  }

  /// <summary>
  /// 씬에 오브젝트 생성
  /// </summary>
  public async UniTask GenerateScene()
  {
    Debug.LogError("GenerateScene");

    for (int i = 0; i < LandDictionary.Keys.Count; i++)
    {
      CityType cityType = LandDictionary.Keys.ElementAt(i);

      Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

      string sceneName = $"{this.scenePath}Scene_Land_{cityType}.unity";

      Debug.Log(sceneName + "생성 중");

      //파일이 존재하면 삭제
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
          $"랜드를 구성중입니다 {i + 1}/{LandDictionary.Count}",
          $"열심히 {cityType} 만드는중 : {j + 1}/{LandDictionary[cityType].Count}",
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
          $"랜드를 굽는중입니다 {i + 1}/{LandDictionary.Count}",
          $"열심히 빵 만드는중...",
          1f
        );

        this.SetMeshBake(bundleMap.transform);
      }

      EditorSceneManager.SaveScene(newScene, sceneName);

      EditorUtility.ClearProgressBar();

      
    }

    EditorUtility.DisplayDialog("작업 완료", "랜드 구성이 완료되었습니다.", "확인");

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
  /// Coordinate값으로 실제 Position 계산하기
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
