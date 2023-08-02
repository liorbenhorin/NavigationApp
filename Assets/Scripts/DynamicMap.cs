// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class dynamic_map : MonoBehaviour
// {
//     // Start is called before the first frame update
//     void Start()
//     {
        
//     }

//     // Update is called once per frame
//     void Update()
//     {
        
//     }
// }

 

using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

public class DynamicMap : MonoBehaviour
{
    public float spacing = 0.1f; // Spacing between each plane

    private List<GameObject> planes = new List<GameObject>();

   void Start()
   {
        string directoryPath = Path.Combine(Application.dataPath, "Resources", "ariel_jpg").Replace('\\', '/');
        print(directoryPath);
        // Get the list of image files from the directory
        string[] imageFiles = Directory.GetFiles(directoryPath, "*.jpg");

        int numRows = 0;
        int numCols = 0;

        GameObject offset_null = new GameObject("offset_null");
        offset_null.transform.parent = transform;

        // Parse the data from image file names
        foreach (string filePath in imageFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string[] nameParts = fileName.Split('_');
            print(fileName);

            if (nameParts.Length != 3)
            {
                Debug.LogWarning($"Invalid image file name: {fileName}. Skipping.");
                continue;
            }

            int col, row;
            if (int.TryParse(nameParts[1], out col) && int.TryParse(nameParts[2], out row))
            {
                numRows = Mathf.Max(numRows, row + 1);
                numCols = Mathf.Max(numCols, col + 1);
            }
        }
        
        // Create the grid of planes
        for (int row = 0; row < numRows; row++)
        {
            for (int col = 0; col < numCols; col++)
            {
                // int textureIndex = row * numCols + col;
                // if (textureIndex >= numTextures)
                // {
                //     break; // No more textures to apply
                // }
                string textureFilePath = Path.Combine(directoryPath, $"square_{col}_{row}.jpg");

                if (!File.Exists(textureFilePath))
                {
                    continue; // Skip if the texture file is missing
                }

                print(String.Format("{0}_{1} ", col, row));
                // Load the texture from file
                byte[] imageData = File.ReadAllBytes(textureFilePath);
                Texture2D texture = new Texture2D(2, 2); // Create a temporary texture
                texture.LoadImage(imageData);

                // Create a new plane GameObject
                GameObject planeObj = GameObject.CreatePrimitive(PrimitiveType.Plane);

                // Position the plane
                float x = col * (-10); // Use width of texture as spacing
                float z = row * (10); // Use height of texture as spacing
                planeObj.transform.position = new Vector3(x, 0f, z);

                // Apply the appropriate texture
                Renderer planeRenderer = planeObj.GetComponent<Renderer>();
                Material unlitMaterial = new Material(Shader.Find("UI/Unlit/Transparent"));
                unlitMaterial.mainTexture = texture;
                planeRenderer.material = unlitMaterial;

                // Scale the plane to match the texture aspect ratio
                float aspectRatio = (float)texture.width / texture.height;
                planeObj.transform.localScale = new Vector3(aspectRatio, 1f, 1f);

                // Parent the plane to the grid builder GameObject
                planeObj.transform.parent = offset_null.transform;

                // Name the plane according to its row and column
                planeObj.name = $"square_{col}_{row}";
                MeshCollider meshCollider = planeObj.GetComponent<MeshCollider>();
                meshCollider.enabled = false;

                // Add the plane to the list for later cleanup
                planes.Add(planeObj);
            }
        }

        offset_null.transform.position = new Vector3(4.76f, 0f, 0.25f);

        transform.localScale = new Vector3(4.67f, 1f, 4.67f);
        transform.Rotate(Vector3.up, 180f);
    }

    void OnApplicationQuit()
    {
        // Clean up created GameObjects and materials
        foreach (var plane in planes)
        {
            Destroy(plane);
        }
        planes.Clear();
    }
}



