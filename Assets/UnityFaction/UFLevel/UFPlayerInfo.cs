﻿using System;
using System.Collections.Generic;
using UFLevelStructure;
using UnityEngine;

public class UFPlayerInfo : MonoBehaviour {

    public string levelName, author;
    public PosRot playerStart;

    public Color defaultAmbient;
    public float ambChangeTime = 10f;

    public Color fogColor;
    public float fogStart, fogEnd;

    public bool multiplayer;
    public SpawnPoint[] spawnPoints;

    public Room[] rooms;
    public Camera skyCamera;


    public void Set(LevelData level, int skyLayer) {
        this.levelName = level.name;
        this.author = level.author;
        this.playerStart = level.playerStart;
        this.multiplayer = level.multiplayer;
        this.spawnPoints = level.spawnPoints;

        this.fogStart = Mathf.Max(0f, level.nearPlane);
        if(level.farPlane <= 0f || fogEnd < fogStart)
            fogEnd = 1000f;
        else
            fogEnd = Mathf.Max(fogStart + 10f, level.farPlane);

        this.defaultAmbient = level.ambientColor;
        this.fogColor = level.fogColor;

        bool foundSkyRoom = false;
        Room skyRoom = default(Room);

        List<Room> roomList = new List<Room>();
        foreach(Room room in level.staticGeometry.rooms) {
            Vector3 roomExtents = room.aabb.max - room.aabb.min;
            Vector3 roomCenter = (room.aabb.max + room.aabb.min)/2f;
            bool realRoom = true;
            if(room.isSkyRoom) {
                foundSkyRoom = true;
                skyRoom = room;
                realRoom = false;
            }
            realRoom &= roomExtents.x > 3f;
            realRoom &= roomExtents.z > 3f;
            realRoom &= roomExtents.y > 1.5f;

            //TODO use life value of rooms
            if(!realRoom)
                continue;
            
            roomList.Add(room);

            MakeEAX(room.eaxEffect, roomCenter, roomExtents.magnitude / 2f);
            
        }
        this.rooms = roomList.ToArray();

        if(foundSkyRoom) {
            GameObject camG = new GameObject("SkyCamera");
            Vector3 skyPos = (skyRoom.aabb.min + skyRoom.aabb.max) / 2f;
            Vector3 skyDiagonal = skyRoom.aabb.max - skyRoom.aabb.min;
            camG.transform.SetParent(transform);
            camG.transform.position = skyPos;
            skyCamera = camG.AddComponent<Camera>();
            skyCamera.depth = -10;
            skyCamera.clearFlags = CameraClearFlags.SolidColor;
            skyCamera.backgroundColor = fogColor;
            skyCamera.farClipPlane = skyDiagonal.magnitude / 2f;
            skyCamera.cullingMask = LayerMask.GetMask(LayerMask.LayerToName(skyLayer));
        }
    }

    private void MakeEAX(Room.EAXEffectType eax, Vector3 roomCenter, float roomRadius) {
        switch(eax) {
        case Room.EAXEffectType.none:
        case Room.EAXEffectType.generic:
        return;
        }

        GameObject g = new GameObject("RoomEAX_" + eax);
        g.transform.SetParent(this.transform);
        g.transform.position = roomCenter;
        AudioReverbZone effect = g.AddComponent<AudioReverbZone>();
        effect.maxDistance = roomRadius;
        effect.minDistance = roomRadius / 2f;
        effect.reverbPreset = GetReverbPreset(eax);
    }

    private AudioReverbPreset GetReverbPreset(Room.EAXEffectType effect) {
        string effectString = UFUtils.Capitalize(effect.ToString());
        if(Enum.IsDefined(typeof(AudioReverbPreset), effectString))
            return (AudioReverbPreset)Enum.Parse(typeof(AudioReverbPreset), effectString);
        
        switch(effect) {
        case Room.EAXEffectType.paddedcell: return AudioReverbPreset.PaddedCell;
        case Room.EAXEffectType.stonecorridor: return AudioReverbPreset.StoneCorridor;
        case Room.EAXEffectType.carpetedhallway: return AudioReverbPreset.CarpetedHallway;
        case Room.EAXEffectType.parkinglot: return AudioReverbPreset.ParkingLot;
        case Room.EAXEffectType.sewerpipe: return AudioReverbPreset.SewerPipe;
        default:
        Debug.LogWarning("Encountered unkown eax effect type: " + effect);
        return AudioReverbPreset.Off;
        }
    }

    private void Start() {
        SetFog();
    }

    private void SetFog() {
        RenderSettings.fog = fogStart > 0f;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
    }

    private void SetRenderSettings() {
        RenderSettings.ambientLight = defaultAmbient;
        SetFog();
    }

    public void ApplyCameraSettings(Camera playerCamera) {
        if(skyCamera != null)
            //rely on sky camera to render sky room
            playerCamera.clearFlags = CameraClearFlags.Depth;
        else if(fogStart > 0f){
            //aply solid color to represent thick fog
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = fogColor;
        }
        else
            //no clearing; time for trippy background effects
            playerCamera.clearFlags = CameraClearFlags.Nothing;

        //far clipping
        playerCamera.farClipPlane = fogEnd;
    }

    public void UpdateCamera(Camera playerCamera) {
        Color targetAmb = defaultAmbient;
        
        Room room;
        if(GetRoom(playerCamera.transform.position, out room)) {
            if(room.hasAmbientLight)
                targetAmb = room.ambientLightColor;

            //TODO apply remaining room effects
        }

        Color currentAmb = RenderSettings.ambientLight;
        float r = Time.deltaTime / ambChangeTime;
        RenderSettings.ambientLight = UFUtils.MoveTowards(currentAmb, targetAmb, r);

        if(skyCamera != null) {
            skyCamera.transform.rotation = playerCamera.transform.rotation;
            skyCamera.fieldOfView = playerCamera.fieldOfView;
        }
    }

    /// <summary>
    /// Returns random position and rotation where to spawn a player of the given class.
    /// </summary>
    public PosRot GetSpawn(PlayerClass playerClass) {
        if(!multiplayer || spawnPoints.Length == 0)
            return playerStart;

        List<SpawnPoint> candidates = new List<SpawnPoint>();
        foreach(SpawnPoint p in spawnPoints) {
            bool valid = false;
            switch(playerClass) {
            case PlayerClass.Free: valid = true; break;
            case PlayerClass.Bot: valid = p.bot; break;
            case PlayerClass.RedTeam: valid = p.redTeam; break;
            case PlayerClass.BlueTeam: valid = p.blueTeam; break;
            }
            if(valid)
                candidates.Add(p);
        }

        if(candidates.Count == 0)
            return GetSpawn(PlayerClass.Free);

        return UFUtils.GetRandom(candidates).transform.posRot;
    }

    

    public string GetLevelInfo() {
        return levelName + ", made by " + author;
    }

    public enum PlayerClass {
        Free, Bot, RedTeam, BlueTeam
    }

    private bool GetRoom(Vector3 position, out Room room) {
        foreach(Room r in rooms) {
            if(r.aabb.IsInside(position)) {
                room = r;
                return true;
            }
        }
        room = default(Room);
        return false;
    }

}
