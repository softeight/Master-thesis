using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    public int instanceCount = 10000;
    public int subMeshIndex = 0;

    // Initialize Buffers
    private ComputeBuffer colorBuffer;
    private ComputeBuffer argsBuffer;
    private ComputeBuffer positionBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    // Variables for checking updates
    private int cachedInstanceCount = -1;
    private int cachedSubMeshIndex = -1;

    public Material material;
    public Color[] colors;
    public Mesh mesh;
    public Vector4 positions;

    public void OnValidate()
    {
        if (material == null)
        {
            Debug.LogError("Material not assigned! Please drag a material into the script.");
            return;
        }

        if (mesh == null)
        {
            Debug.LogError("Mesh not assigned! Please drag a mesh into the script.");
            return;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        UpdateBuffers();

        
    }

    // Update is called once per frame
    void Update()
    {
        if (cachedInstanceCount != instanceCount || cachedSubMeshIndex != subMeshIndex)
        { 
            UpdateBuffers(); 
        }

        Graphics.DrawMeshInstancedIndirect(mesh,subMeshIndex,material,new Bounds(Vector3.zero,new Vector3(100.0f,100.0f,100.0f)),argsBuffer);
        
    }

    void UpdateBuffers()
    {
        //GetComponent<MeshRenderer>().material = material;

        if (positionBuffer != null) {positionBuffer.Release();}
        positionBuffer = new ComputeBuffer(instanceCount, 4 * sizeof(float));
        Vector4[] positions = new Vector4[instanceCount];
        
        // Give random positions
        for(int i = 0; i < instanceCount; i++)
        {
            float angle = Random.Range(0.0f, Mathf.PI * 2.0f);
            float distance = Random.Range(20.0f, 100.0f);
            float height = Random.Range(-2.0f, 2.0f);
            float size = 1.0f;
            positions[i] = new Vector4(Mathf.Sin(angle) * distance, height, Mathf.Cos(angle) * distance, size);
        
        }
        positionBuffer.SetData(positions);
        material.SetBuffer("_Positions", positionBuffer);

        // Set Color Data and bind to the GPU Buffer
        if (colorBuffer != null) { colorBuffer.Release(); }
        colorBuffer = new ComputeBuffer(instanceCount, 4 * sizeof(float));
        colors = new Color[instanceCount];
        
        // Give random color value
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = new Color(Random.value, Random.value, Random.value, 1f);

        }
        colorBuffer.SetData(colors);
        material.SetBuffer("_Colors", colorBuffer);

        // Indirect Args
        if(mesh != null)
        {
            args[0] = (uint)mesh.GetIndexCount(subMeshIndex);
            args[1] = (uint)instanceCount;
            args[2] = mesh.GetIndexStart(subMeshIndex);
            args[3] = mesh.GetBaseVertex(subMeshIndex);
        }
        else
        {
            args[0] = args[1] = args[2] = args[3] = 0;
        }

        argsBuffer.SetData(args);

        //cachedInstanceCount = instanceCount;
        cachedSubMeshIndex = subMeshIndex;
        
    }

    void OnDisable()
    {
        if (positionBuffer != null)
            positionBuffer.Release();
        positionBuffer = null;

        if (argsBuffer != null)
            argsBuffer.Release();
        argsBuffer = null;

        if (colorBuffer != null)
            colorBuffer.Release();
        colorBuffer = null;
    }
}
