using UnityEngine;
using System;


public class PointCloudRenderer : MonoBehaviour
{
	void setup () 
	{
		Mesh mesh = new Mesh ();
		GetComponent<MeshFilter> ().mesh = mesh;
	}

    public void UpdateMesh(Vector3[] vertices, Color[] colors, Matrix4x4 matr)
	{
		Mesh mesh = GetComponent<MeshFilter> ().mesh;
		mesh.Clear ();
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // using Unity 2017.3 32 bit mesh index buffers

		int[] indices = new int[vertices.Length];

		for (int i = 0; i < vertices.Length; i++)
        {
			//vertices [i] = matr.MultiplyPoint3x4 (vertices [i]);
			indices [i] = i;
        }

		mesh.vertices = vertices;
		mesh.colors = colors;
		mesh.SetIndices (indices, MeshTopology.Points, 0);
    }

	public void ClearMesh () 
	{
		Mesh mesh = GetComponent<MeshFilter> ().mesh;
		mesh.Clear ();
	}
}
