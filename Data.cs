using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static SceneGenerateTool;
using DigitalOpus.MB.Core;

#region Enum Data
[System.Serializable]
public struct MetaCityData
{
  public CityType cityType;
  public CameraData cameraData;
}

[System.Serializable]
public struct CameraData
{
  public Vector3 position;
  public Vector3 rotation;
}

#region NavMesh
[System.Serializable]
public enum NavMeshType
{
  BundleRoad,
  BundleWalkWay,
  BundleExcept,
  BundleLand,
  BundleOther
}

[System.Serializable]
public struct NavMeshTypeData
{
  public NavMeshType type;
  public string[] containName;
}
#endregion

#endregion



[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/Data", order = 1)]
public class Data : ScriptableObject
{
  public GameObject meshBaker;
  public GameObject NavMeshBaker;


  [Header("NavMesh Bake Data")]
  //특정 이름을 가지고 있는 오브젝트들 Type별로 Hierachy 부모 설정 및 SetParent
  public List<NavMeshTypeData> navMeshDataList;
}
