using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DigitalOpus.MB.Core;
using System.Linq;

public class MeshBakerTool : MonoBehaviour 
{


  public static void MeshBaker(GameObject prefabMeshBaker, Transform bundleMap, MeshBakeType meshBakeType)
  {
    MeshRenderer[] gameObjectList = bundleMap.GetComponentsInChildren<MeshRenderer>();

    switch (meshBakeType)
    {
      case MeshBakeType.SharedMaterial:
        MeshBakerSharedMaterial(prefabMeshBaker, gameObjectList);
        break;
      case MeshBakeType.All:
        MeshBakerAll(prefabMeshBaker, gameObjectList);
        break;
    }

  }

  private static void MeshBakerSharedMaterial(GameObject prefabMeshBaker, MeshRenderer[] meshRendererList)
  {
    Dictionary<Material, GameObject[]> meshBakeringList = meshRendererList.GroupBy(m => m.sharedMaterial)
                                                                        .ToDictionary(n => n.Key, n => n.Select(m => m.gameObject)
                                                                        .ToArray());

    foreach (var meshBakering in meshBakeringList)
    {
      MB3_MeshBaker meshBaker = Instantiate(prefabMeshBaker).GetComponent<MB3_MeshBaker>();

      ((MB3_MeshCombinerSingle)meshBaker.meshCombiner).SetMesh(null);

      //Add the objects to the combined mesh
      if (meshBaker.AddDeleteGameObjects(meshBakering.Value, meshBakering.Value, true))
      {
        meshBaker.Apply();

        DestroyImmediate(meshBaker.gameObject);
      }

    }
  }

  private static void MeshBakerAll(GameObject prefabMeshBaker, MeshRenderer[] meshRendererList)
  {
    MB3_MeshBaker meshBaker = Instantiate(prefabMeshBaker).GetComponent<MB3_MeshBaker>();

    ((MB3_MeshCombinerSingle)meshBaker.meshCombiner).SetMesh(null);

    GameObject[] gameObjectList = meshRendererList.Select(n => n.gameObject).ToArray();

    //Add the objects to the combined mesh
    if (meshBaker.AddDeleteGameObjects(gameObjectList, gameObjectList, true))
    {
      meshBaker.Apply();

      DestroyImmediate(meshBaker.gameObject);
    }
  }
}
