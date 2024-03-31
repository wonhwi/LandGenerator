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
#endregion

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/Data", order = 1)]
public class Data : ScriptableObject
{
  public GameObject meshBaker;
  public List<MetaCityData> metaCityDataList;

  public MetaCityData GetMetaCityData(CityType cityType)
    => metaCityDataList.Find(n => n.cityType.ToString().Equals(cityType.ToString()));
}
