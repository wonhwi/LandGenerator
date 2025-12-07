using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AI;

[CustomEditor(typeof(NavMeshBakerTool))]
public class NavMeshBakerToolEditor : Editor
{
  public override void OnInspectorGUI()
  {
    DrawDefaultInspector();
    EditorGUILayout.Space(30);
    GUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();
    if (GUILayout.Button("NavMeshBake 실행", GUILayout.Width(200f), GUILayout.Height(40f)))
    {
      ((NavMeshBakerTool)target).ExcuteNavMeshBake();
    }
    if (GUILayout.Button("AllClear", GUILayout.Width(200f), GUILayout.Height(40f)))
    {
      //NavMesh 삭제 및 사용된 오브젝트 전부 삭제
      ((NavMeshBakerTool)target).AllClear();
    }
    GUILayout.FlexibleSpace();
    GUILayout.EndHorizontal();
  }
}
#endif


public class NavMeshBakerTool : MonoBehaviour
{
  //Scriptable Object
  public Data dataMap;

  [Header("NavMeshSurface")]
  public NavMeshSurface HumanoidNavSurface;
  public NavMeshSurface VehicleNavSurface;

  [Header("Bundle NavMeshModifier")]
  public SerializableDictionary<NavMeshType, NavMeshModifier> bundleNavMeshModifier = new SerializableDictionary<NavMeshType, NavMeshModifier>();
  [Header("오브젝트들의 최상단 오브젝트 (예 : Map)")]
  public GameObject target;


  [ContextMenu("Excute NavMeshBake")]
  public async UniTask ExcuteNavMeshBake()
  {
    AllClear();

    if(target == null)
      target = GameObject.Find("Map");

    await this.CopyNavMeshObject();

    await this.BakeBundle(HumanoidNavSurface);
    await this.BakeBundle(VehicleNavSurface);

    //EditorUtility.ClearProgressBar();
    //EditorUtility.DisplayDialog("작업 완료", "NavMesh Bake가 완료되었습니다.", "확인");
  }

  /// <summary>
  /// SceneGeneratorTool이 생성한 Map의 하위 오브젝트들 복사
  /// </summary>
  /// <returns></returns>
  public async UniTask CopyNavMeshObject()
  {
    target.SetActive(true);

    foreach (GameObject obj in FindObjectsOfType(typeof(GameObject)))
    {
      int findIndex = dataMap.navMeshDataList.FindIndex(n => n.containName.Any(m => obj.name.Contains(m)));

      if (findIndex.Equals(-1))
        continue;

      GameObject instanceObject = GameObject.Instantiate(obj);

      NavMeshType navMeshType = dataMap.navMeshDataList[findIndex].type;

      instanceObject.transform.SetPositionAndRotation(obj.transform.position, obj.transform.rotation);
      instanceObject.transform.SetParent(bundleNavMeshModifier[navMeshType].transform);

    }

    target.SetActive(false);
  }

  /// <summary>
  /// NavMeshSurface Bake 및 await 대기
  /// </summary>
  /// <param name="surface"></param>
  /// <returns></returns>
  public async UniTask BakeBundle(NavMeshSurface surface)
  {
    NavMeshAssetManager.instance.StartBakingSurfaces(new UnityEngine.Object[] { surface });

    while (surface.navMeshData == null)
      await UniTask.Delay(1000);
  }

  [ContextMenu("AllClear")]
  public void AllClear()
  {
    HumanoidNavSurface.RemoveData();
    VehicleNavSurface.RemoveData();

    foreach (var navMeshModifier in bundleNavMeshModifier)
    {
      Transform transform = navMeshModifier.Value.transform;
      int childCount = transform.childCount;

      if (childCount.Equals(0))
        continue;

      for (int i = childCount - 1; i >= 0; i--)
      {
        DestroyImmediate(transform.GetChild(i).gameObject);
      }
    }

    NavMeshAssetManager.instance.ClearSurfaces(new UnityEngine.Object[] { HumanoidNavSurface });
    NavMeshAssetManager.instance.ClearSurfaces(new UnityEngine.Object[] { VehicleNavSurface });

    SceneView.RepaintAll();
  }

  /// <summary>
  /// 카메라 위치 설정을 위한 부모 중앙 값 반환
  /// </summary>
  /// <returns></returns>
  public Vector3 GetCenterPos()
  {
    List<Transform> allChildren = new List<Transform>();
    CollectChildrenRecursive(transform, allChildren);

    if (allChildren.Count == 0)
      return transform.position;

    Bounds bounds = new Bounds(allChildren[0].position, Vector3.zero);
    foreach (var child in allChildren)
      bounds.Encapsulate(child.position);

    return bounds.center;
  }

  void CollectChildrenRecursive(Transform current, List<Transform> list)
  {
    foreach (Transform child in current)
    {
      list.Add(child);
      CollectChildrenRecursive(child, list);
    }
  }
}
