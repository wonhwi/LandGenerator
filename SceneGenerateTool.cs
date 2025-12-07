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
  None = 0,   //사용안함
  All = 1,    //모두 하나로 Combine
  SharedMaterial = 2 //공용 재질사용하는 오브젝트들끼리 Combine
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
    DrawDefaultInspector();

    EditorGUILayout.Space(10);
    GUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();
    if (GUILayout.Button("랜드 구성", GUILayout.Width(200f), GUILayout.Height(40f)))
    {
      sceneGenerateTool.StartGenerateScene().Forget();
    }
    GUILayout.FlexibleSpace();
    GUILayout.EndHorizontal();
  }
}

#endregion

public class SceneGenerateTool : MonoBehaviour
{
  

  private Dictionary<CityType, List<LandGeneratorData>> LandDictionary = new Dictionary<CityType, List<LandGeneratorData>>();
  private Dictionary<string, (long landIdx, string landCode)> EmptyPathList = new Dictionary<string, (long, string)>();


  private Dictionary<string, GameObject> PrefabPathDictionary = new Dictionary<string, GameObject>();


  [Header("랜드 구성 관련 데이터")]
  /// <summary>
  /// ScriptableObject Data
  /// </summary>
  public Data dataMap;

  
  [Header("랜드 구성 관련 설정 옵션")]

  /// <summary>
  /// 씬 구성 할 씬 타입들
  /// </summary>
  [Tooltip("씬 구성 할 씬 타입들")]
  public CityType cityType;


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
    if (cityType == CityType.None)
    {
      Debug.LogError("City 선택을 해주세요");
      return;
    }

    await this.LoadPrefabData();

    await this.LoadData();

    await this.GenerateScene();
  }

  /// <summary>
  /// 프리팹 데이터 저장
  /// </summary>
  /// <returns></returns>
  public async UniTask LoadPrefabData()
  {
    //프리팹 관련 정보들 저장
    EmptyPathList.Clear();
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

            Debug.Log($"생성할 랜드 이름 : {value} = {landData.Where(n => n.Land_Code.Equals(value.ToString())).ToList().Count}");
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

    if(LandDictionary.Keys.Count.Equals(0))
    {
      Debug.LogError("값이없어 확인해봐");
    }  

    for (int i = 0; i < LandDictionary.Keys.Count; i++)
    {
      CityType cityType = LandDictionary.Keys.ElementAt(i);

      Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

      string sceneName = $"{this.scenePath}Scene_Land_{cityType}.unity";

      Debug.Log(sceneName + "생성 중");

      //씬을 미리 생성해둬야 NavMeshBake Data가 해당 폴더에 정상적으로 들어감
      EditorSceneManager.SaveScene(newScene, sceneName);

      //파일이 존재하면 삭제
      if (File.Exists(sceneName))
        AssetDatabase.DeleteAsset(sceneName);

      GameObject bundleMap = new("Map");

      for (int j = 0; j < LandDictionary[cityType].Count; j++)
      {
        LandGeneratorData landData = LandDictionary[cityType][j];

        if (string.IsNullOrEmpty(landData.land_model))
          continue;

        //프리팹 경로 없는것 찾아서 Log띄워주는 Dictionary
        if (!PrefabPathDictionary.ContainsKey(landData.land_model))
        {
          if(!EmptyPathList.ContainsKey(landData.land_model))
            EmptyPathList.Add(landData.land_model, (landData.index, landData.Land_Code));
          continue;
        }
          

        EditorUtility.DisplayProgressBar(
          $"랜드를 구성중입니다 {i + 1}/{LandDictionary.Count}",
          $"열심히 {cityType} 만드는중 : {j + 1}/{LandDictionary[cityType].Count}",
          (float)j / (float)LandDictionary[cityType].Count);

        GameObject createdObject = PrefabUtility.InstantiatePrefab(PrefabPathDictionary[landData.land_model], bundleMap.transform) as GameObject;

        //Debug.LogError($"{createdObject != null} \n {JsonConvert.SerializeObject(landData)}");

        //await UniTask.WaitUntil(() => createdObject != null);

        createdObject.name += $" {landData.index} : ({(landData.CoordiStartX + landData.CoordiEndX) * 0.5f},{(landData.CoordiStartY + landData.CoordiEndY) * 0.5f})";
        createdObject.transform.SetPositionAndRotation(
          CalculationCoordinate(landData), 
          Quaternion.Euler(0f, (landData.land_rotation - 1) * -90, 0f)
          );

      }

      #region NavMeshBake
      EditorUtility.ClearProgressBar();

      GameObject navMeshBaker = PrefabUtility.InstantiatePrefab(this.dataMap.NavMeshBaker) as GameObject;

      navMeshBaker.transform.SetAsLastSibling();

      NavMeshBakerTool navMeshBakerTool = navMeshBaker.GetComponent<NavMeshBakerTool>();

      EditorUtility.DisplayProgressBar(
        $"NavMeshBake",
        $"NavMeshBaking...",
        1f
      );

      await navMeshBakerTool.ExcuteNavMeshBake();

      this.SetCamera(navMeshBakerTool.GetCenterPos());
      #endregion

      #region MeshBake
      if (this.meshBakeType != MeshBakeType.None)
      {
        EditorUtility.ClearProgressBar();

        EditorUtility.DisplayProgressBar(
          $"Mesh를 하나로 만드는 중입니다 {i + 1}/{LandDictionary.Count}",
          $"MeshBaking...",
          1f
        );

        this.SetMeshBake(bundleMap.transform, meshBakeType);

        await UniTask.Delay(5000);
      }
      #endregion

      //최종 작업 이후 저장
      EditorSceneManager.SaveScene(newScene, sceneName);

      EditorUtility.ClearProgressBar();

    }

    foreach (var emptyPath in EmptyPathList)
    {
      Debug.LogError($"프리팹 = {emptyPath.Key} 가 없습니다.");
    }

    EditorUtility.DisplayDialog("작업 완료", "랜드 구성이 완료되었습니다.", "확인");
  }
  
  private void SetCamera(Vector3 position)
  {
    Camera cam = Camera.main;

    if (cam)
    {
      cam.transform.SetPositionAndRotation(new Vector3(position.x, position.y + 3000f, position.z), Quaternion.Euler(90f, 0f, 0f));

      cam.farClipPlane = 10000f;

    }
  }

  private void SetMeshBake(Transform bundleMap, MeshBakeType meshBakeType)
  {
    MeshBakerTool.MeshBaker(this.dataMap.meshBaker, bundleMap, meshBakeType);

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

  [ContextMenu("DisableWindow")]
  private void DisableWindow()
  {
    EditorUtility.ClearProgressBar();
  }

}
