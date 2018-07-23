﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class V3DReader {

    /* A V3D file encodes a 3D model, including mesh, uv unwrapping and rig.
     * (Volition 3D?) The file appears with V3D, V3M and V3C extensions.
     * Note that some data tends to be missing in these files for whatever reason,
     * including the byte length of each section.
     * 
     * A V3D file is used to encode clutter, entities and items 
     * that are used repeatedly in different levels. The file consists of a short 
     * header followed by a series of sections, each of which may be repeated.
     * Sections include submeshes, bones and collider data.
     * Each submesh contains a full mesh and a few simplified meshes (different LOD)
     * that are usually deployed when an object is far from the camera. 
     * Each lod is in turn split up in a series of submeshes that constitie seperate objects.
     * This makes the mesh hiararchy V3D > SubMesh > LOD > Batch
     * Where only Batches contain geometry data, including vertices, faces and UV's
     * 
     * The V3D is processed by the V3DReader by building a prefab containing all the submeshes.
     * This prefab is built into the scene, then saved, then deleted from the scene, 
     * leaving only the prefab asset as the result.
     */

    [MenuItem("UnityFaction/Convert V3D")]
    public static void ConvertV3D() {

        //let user select vpp folder he would like to unpack
        string fileSearchMessage = "Select V3D file you would like to convert into a prefab.";
        string defaultPath = "Assets";
        if(!string.IsNullOrEmpty(lastV3DPath))
            defaultPath = Path.GetDirectoryName(lastV3DPath);
        string filePath = EditorUtility.OpenFilePanel(fileSearchMessage, defaultPath, "v3d,v3m,v3c");
        if(string.IsNullOrEmpty(filePath))
            return;
        if(!UFUtils.IsAssetPath(filePath)) {
            Debug.LogError("Can only convert files that are in an Asset folder. Please move the file into your Unity project and try again.");
            return;
        }

        lastV3DPath = filePath;

        V3DReader reader = new V3DReader(filePath);
        reader.MakePrefabAtAssetPath();
    }

    private static string lastV3DPath;

    //V3D properties
    const uint V3M_SIGNATURE = 0x52463344;
    const uint V3C_SIGNATURE = 0x5246434D;
    const uint V3D_VERSION = 0x40000;

    private enum V3DSection {
        V3D_END = 0x00000000,
        V3D_SUBMESH = 0x5355424D,
        V3D_COLSPHERE = 0x43535048,
        V3D_BONE = 0x424F4E45,
        V3D_DUMB = 0x44554D42,
    };

    //file structure parameters
    private string assetPath;
    private int pointer;
    private int nboSubMeshes, nboColSpheres;

    //output parameters
    private string fileName;
    private Material[] materials;
    private GameObject g;

    /// <summary>
    /// Make reader from the given file path, read the file and use it to generate
    /// a gameobject from the V3D into the current Unity scene.
    /// </summary>
    public V3DReader(string path) {
        byte[] bytes = File.ReadAllBytes(path);
        fileName = Path.GetFileNameWithoutExtension(path);

        string inputDirectory;
        if(UFUtils.IsAssetPath(path))
            inputDirectory = UFUtils.GetRelativeUnityPath(Path.GetDirectoryName(path));
        else
            inputDirectory = Path.GetDirectoryName(path);

        assetPath = inputDirectory + "/" + VPPUnpacker.assetFolder + "/";

        if(!Directory.Exists(assetPath))
            AssetDatabase.CreateFolder(inputDirectory, VPPUnpacker.assetFolder);

        MakeGameObjectFromV3D(bytes);
    }

    /// <summary>
    /// Make a prefab from the gameObject generated by this Reader 
    /// and delete the old gameObject from the current scene.
    /// </summary>
    public void MakePrefabAtAssetPath() {
        GeneratePrefab(assetPath);
    }

    /// <summary>
    /// Generate a gameobject from the given V3D data.
    /// </summary>
    private void MakeGameObjectFromV3D(byte[] bytes) {
        pointer = 0;
        ReadHeader(bytes);

        uint nextSection = BitConverter.ToUInt32(bytes, pointer);

        int subMeshCount = 0;
        int colSphereCount = 0;

        GameObject[] meshObjects = new GameObject[nboSubMeshes];

        while(nextSection != (uint)V3DSection.V3D_END) {

            switch((V3DSection)nextSection) {
            case V3DSection.V3D_SUBMESH:
            meshObjects[subMeshCount++] = ReadSubMesh(bytes);
            break;

            case V3DSection.V3D_COLSPHERE:
            colSphereCount++; 
            ReadColSphere(bytes);
            break;

            case V3DSection.V3D_BONE: ReadBones(bytes); break;
            case V3DSection.V3D_DUMB: ReadDumb(bytes); break;

            default:
            Debug.LogError("Encountered unkown V3D section: " + UFUtils.GetHex((int)nextSection));
            return;
            }

            nextSection = BitConverter.ToUInt32(bytes, pointer);
        }

        if(subMeshCount != nboSubMeshes)
            Debug.LogError("Could not parse correct amount of submeshes.");
        if(colSphereCount != nboColSpheres)
            Debug.LogError("Could not parse correct amount of collider spheres.");
        if(pointer + 8 != bytes.Length)
            Debug.LogError("V3D parsing ended in invalid state. Resulting file may be corrupted.");

        //TODO use bones and collider data
        g = MakeMeshObject(meshObjects, fileName);
    }

    /// <summary>
    /// Extract general info of the RFL contained in its header.
    /// </summary>
    private void ReadHeader(byte[] bytes) {
        int signature = BitConverter.ToInt32(bytes, pointer);
        int version = BitConverter.ToInt32(bytes, pointer + 4);

        if(signature != V3C_SIGNATURE && signature != V3M_SIGNATURE)
            throw new Exception("File did not have V3C or V3M signature");
        else if(version != V3D_VERSION)
            Debug.LogWarning("V3D file had unkown version, trying to parse anyway...");

        nboSubMeshes = BitConverter.ToInt32(bytes, pointer + 8);
        //total vertex count, total triangle count, unknown (null)
        //nboMaterials = BitConverter.ToInt32(bytes, pointer + 24);
        //two more unkowns (null)
        nboColSpheres = BitConverter.ToInt32(bytes, pointer + 36);
        pointer += 40;
    }

    /// <summary>
    /// Read SubMesh data and compule it into a gameObject to be parented under the prefab.
    /// </summary>
    private GameObject ReadSubMesh(byte[] bytes) {
        pointer += 8; //header and size, size often set to 0

        string name = UFUtils.ReadNullTerminatedString(bytes, pointer);
        pointer += 24;
        UFUtils.ReadNullTerminatedString(bytes, pointer);
        pointer += 24;

        //int version = BitConverter.ToInt32(bytes, pointer);
        int nboLod = BitConverter.ToInt32(bytes, pointer + 4);
        pointer += 8;

        float[] lodDistances = new float[nboLod];
        for(int i = 0; i < nboLod; i++) {
            lodDistances[i] = BitConverter.ToInt32(bytes, pointer);
            pointer += 4;
        }

        //Bounding sphere
        //Vector3 sphereCenter = UFUtils.Getvector3(bytes, pointer);
        //float sphereRadius = BitConverter.ToSingle(bytes, pointer + 12);
        pointer += 16;

        //axis aligned bounding box
        //Vector3 boundMin = UFUtils.Getvector3(bytes, pointer);
        //Vector3 boundMax = UFUtils.Getvector3(bytes, pointer + 12);
        pointer += 24;

        Mesh mesh = null;
        string[] textures = null;
        int highestNboTriangles = 0;

        for(int i = 0; i < nboLod; i++) {
            //flags
            int flags = BitConverter.ToInt32(bytes, pointer);
            bool unkownLodFlag1 = (flags & 0x20) != 0;
            bool unkownLodFlag2 = (flags & 0x1) != 0;
            int nboUnkownLodObj = BitConverter.ToInt32(bytes, pointer + 4);
            //unkown (integer)
            pointer += 8;

            short nboBatches = BitConverter.ToInt16(bytes, pointer);
            pointer += 2;

            int dataSize = BitConverter.ToInt32(bytes, pointer);
            pointer += 4;

            //lod mesh data
            int meshPointer = pointer;
            pointer += dataSize;

            pointer += 4; //unkown (-1)

            BatchInfo[] batches = new BatchInfo[nboBatches];
            int totalTriangles = 0;
            for(int j = 0; j < nboBatches; j++) {
                batches[j] = ReadBatch(bytes);
                totalTriangles += batches[j].nboTriangles;
            }

            int nboProps = BitConverter.ToInt32(bytes, pointer);
            int nboTextures = BitConverter.ToInt32(bytes, pointer + 4);
            pointer += 8;

            string[] nextTextures = new string[nboTextures];
            for(int j = 0; j < nboTextures; j++) {
                //byte id = bytes[pointer];
                pointer += 1;
                nextTextures[j] = UFUtils.ReadNullTerminatedString(bytes, ref pointer);
            }

            //update mesh if the level of detail is higher than the current record
            if(totalTriangles > highestNboTriangles) {
                mesh = ReadMesh(bytes, meshPointer, batches, nboProps, unkownLodFlag1, unkownLodFlag2, nboUnkownLodObj);
                highestNboTriangles = totalTriangles;
                textures = nextTextures;
            } 
        }

        int nboMaterials = BitConverter.ToInt32(bytes, pointer);
        pointer += 4;

        //TODO: expand texture list with material specific information
        //Material[] materials = new Material[nboMaterials];
        for(int i = 0; i < nboMaterials; i++) {
            
            //string diffuseTexture = UFUtils.ReadNullTerminatedString(bytes, pointer);
            pointer += 32;

            //4 coefficients, 4th one may be related to reflection value
            pointer += 16;

            //string refTexture = UFUtils.ReadNullTerminatedString(bytes, pointer);
            pointer += 32;

            //bool twoSided = UFUtils.GetFlag(bytes, pointer, 1);
            pointer += 4; //more flags available with unkown purpose
        }

        int nboUnkown = BitConverter.ToInt32(bytes, pointer);
        pointer += 4;
        for(int i = 0; i < nboUnkown; i++) {
            //UFUtils.ReadNullTerminatedString(bytes, pointer);
            pointer += 28; //submesh name trailed by 4xnull
        }

        GameObject toReturn = MakeMeshObject(mesh, name, textures);

        return toReturn;
    }

    private void ReadBones(byte[] bytes) {
        pointer += 8;

        int nboBones = BitConverter.ToInt32(bytes, pointer);
        pointer += 4;
        for(int i = 0; i < nboBones; i++) {
            //UFUtils.ReadNullTerminatedString(bytes, pointer);
            pointer += 24;
            //Quaternion rotation = UFUtils.GetQuaternion(bytes, pointer);
            //Vector3 position = UFUtils.Getvector3(bytes, pointer + 16);
            //int parent = BitConverter.ToInt32(bytes, pointer + 28); //-1 means root 
            pointer += 32;
        }
    }

    private void ReadDumb(byte[] bytes) {
        pointer += 8 + 24 + 32;
    }

    private void ReadColSphere(byte[] bytes) {
        pointer += 8;

        //string name = UFUtils.ReadNullTerminatedString(bytes, pointer);
        pointer += 24;

        //int bone = BitConverter.ToInt32(bytes, pointer); //-1 if not existant
        //Vector3 relPos = UFUtils.Getvector3(bytes, pointer + 4);
        //float radius = BitConverter.ToSingle(bytes, pointer + 16);
        pointer += 20;
    }

    /// <summary>
    /// Read info contained in a BatchHeader and return it.
    /// </summary>
    private BatchInfo ReadBatch(byte[] bytes) {
        BatchInfo toReturn;

        toReturn.nboVertices = BitConverter.ToInt16(bytes, pointer);
        toReturn.nboTriangles = BitConverter.ToInt16(bytes, pointer + 2);
        toReturn.nboPositions = BitConverter.ToInt16(bytes, pointer + 4);
        toReturn.nboIndices = BitConverter.ToInt16(bytes, pointer + 6);
        toReturn.unkn1 = BitConverter.ToInt16(bytes, pointer + 8);
        toReturn.nboBoneLinks = BitConverter.ToInt16(bytes, pointer + 10);
        toReturn.nboTexCoords = BitConverter.ToInt16(bytes, pointer + 12);
        toReturn.unkn2 = BitConverter.ToInt16(bytes, pointer + 14);
        toReturn.unkn3 = BitConverter.ToInt16(bytes, pointer + 16);

        pointer += 18;

        return toReturn;
    }

    private struct BatchInfo {
        public short nboVertices, nboTriangles, nboPositions, nboIndices, 
            unkn1, nboBoneLinks, nboTexCoords, unkn2, unkn3;

        public override string ToString() {
            return "v:" + nboVertices + ", t:" + nboTriangles + ", p:" + nboPositions
                 + ", i:" + nboIndices + ", u1:" + unkn1 + ", bl:" + nboBoneLinks
                  + ", tc:" + nboTexCoords + ", u2:" + unkn2 + ", u3:" + unkn3;
        }
    }

    /// <summary>
    /// Read mesh data contained in a Batch and return it inside a Unity mesh object.
    /// </summary>
    private Mesh ReadMesh(byte[] bytes, int start, BatchInfo[] batches, 
        int nboProps, bool unkownLodFlag1 , bool unkownLodFlag2 , int nboUnkownLodObj) {

        int pointer = start;
        int nboBatches = batches.Length;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        int[][] triangles = new int[nboBatches][];
        int idxOffset = 0;


        for(int i = 0; i < nboBatches; i++) {
            //batch headers, duplicate (or wrong?) of BatchInfo
            pointer += 56;
        }

        Pad(start, ref pointer);

        for(int i = 0; i < nboBatches; i++) {
            //batch data

            BatchInfo batch = batches[i];

            for(int j = 0; j < batch.nboVertices; j++) {
                vertices.Add(UFUtils.Getvector3(bytes, pointer));
                pointer += 12;
            }

            Pad(start, ref pointer);

            //Normals can be recalculated, throw them away
            //Vector3[] normals = new Vector3[batch.nboVertices];
            for(int j = 0; j < batch.nboVertices; j++) {
                //normals[j] = UFUtils.Getvector3(bytes, pointer);
                pointer += 12;
            }

            Pad(start, ref pointer);

            for(int j = 0; j < batch.nboVertices; j++) {
                Vector2 nextUV = UFUtils.Getvector2(bytes, pointer);
                nextUV = new Vector2(nextUV.x, -nextUV.y);
                uvs.Add(nextUV);
                pointer += 8;
            }

            Pad(start, ref pointer);

            triangles[i] = new int[3 * batch.nboTriangles];
            for(int j = 0; j < batch.nboTriangles; j++) {
                for(int k = 0; k < 3; k++) {
                    int nextVertRef = BitConverter.ToInt16(bytes, pointer + (2 * k)) + idxOffset;
                    triangles[i][3 * j + k] = nextVertRef;
                }
                    

                pointer += 8; //unkown; flag or padding
            }

            idxOffset = vertices.Count;

            Pad(start, ref pointer);

            if(unkownLodFlag1) {
                pointer += 4 * batch.nboTriangles * 4;
                Pad(start, ref pointer);
            }

            pointer += batch.unkn1;

            Pad(start, ref pointer);

            //TODO use bones
            if(batch.nboBoneLinks > 0){
                for(int j = 0; j < batch.nboVertices; j++) {
                    /*
                    byte[] weights = new byte[4];
                    byte[] bones = new byte[4];
                    for(int k = 0; k < 4; k++) {
                        weights[k] = bytes[pointer + k];
                        weights[k] = bytes[pointer + 4 + k]; // -1 indicates unused
                    }
                    */
                    pointer += 8;


                }
                Pad(start, ref pointer);
            }

            if(unkownLodFlag2) {
                pointer += 2 * nboUnkownLodObj;
                Pad(start, ref pointer);
            }
        }
        Pad(start, ref pointer);
        for(int i = 0; i < nboProps; i++) {
            //props
            pointer += 68 + 7 + 4; //name, (PosRot) and -1
        }

        return MakeMesh(vertices, uvs, triangles);
    }

    /// <summary>
    /// Move up the pointer so as to correspond to the PAD_LENGTH.
    /// </summary>
    private void Pad(int start, ref int pointer) {
        int PAD_LENGTH = 16;
        int overshoot = (pointer - start) % PAD_LENGTH;
        if(overshoot > 0)
            pointer += PAD_LENGTH - overshoot;
    }

    /// <summary>
    /// Compile the given subMeshe GameObject into a single object.
    /// </summary>
    private GameObject MakeMeshObject(GameObject[] subMeshes, string name) {
        GameObject g = new GameObject(name);

        foreach(GameObject sg in subMeshes)
            sg.transform.SetParent(g.transform);

        return g;
    }

    /// <summary>
    /// Make a mesh object containing the given mesh.
    /// This mesh object is automatically saved as an asset so 
    /// we can continue to use it in prefabs.
    /// </summary>
    private GameObject MakeMeshObject(Mesh mesh, string name, string[] textures) {
        GameObject g = new GameObject(name);

        MeshFilter mf = g.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        Material[] materials = LevelBuilder.GetMaterials(textures, assetPath);
        MeshRenderer mr = g.AddComponent<MeshRenderer>();
        mr.materials = materials;
        mesh.name = name;

        //export mesh so we can continue to refer to it in prefabs
        string meshPath = assetPath + fileName + "_subMesh_" + name + ".asset";
        AssetDatabase.CreateAsset(mesh, meshPath);
        mf.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        return g;
    }

    /// <summary>
    /// Make a mesh from the given geometry data.
    /// </summary>
    private static Mesh MakeMesh(List<Vector3> vertices, List<Vector2> uvs, int[][] triangles) {
        Mesh mesh = new Mesh();

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);

        mesh.subMeshCount = triangles.Length;
        for(int i = 0; i < triangles.Length; i++) {
            mesh.SetTriangles(triangles[i], i);
        }

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Converts associated game object into a prefab saved at the given location. 
    /// The prefab will have the same name as this object.
    /// (The original gameObject will be destroyed)
    /// </summary>
    public void GeneratePrefab(string exportFolder) {
        string path = exportFolder + g.name + ".prefab";
        UnityEngine.Object prefab = PrefabUtility.CreateEmptyPrefab(path);
        PrefabUtility.ReplacePrefab(g, prefab, ReplacePrefabOptions.ConnectToPrefab);
        GameObject.DestroyImmediate(g);
    }
}
