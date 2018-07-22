﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UFLevelStructure;

public class UFLiquid : MonoBehaviour {

    public Room.LiquidProperties.LiquidType type;
    public float visibility;
    public float alpha;
    public Color color;

    private UFPlayerMovement player;

    public void Set(Room room) {
        Vector3 center = (room.aabb.min + room.aabb.max) / 2f;
        Vector3 extents = room.aabb.max - room.aabb.min;

        Room.LiquidProperties liquid = room.liquidProperties;

        float depth = liquid.depth;
        float y = room.aabb.min.y + depth / 2f;

        BoxCollider bc = gameObject.AddComponent<BoxCollider>();
        bc.center = new Vector3(center.x, y, center.z);
        bc.size = new Vector3(extents.x, depth, extents.z);
        bc.isTrigger = true;

        alpha = liquid.alpha / 255f;
        color = liquid.color;
        type = liquid.type;
        visibility = liquid.visibility;
    }

    private void Update() {
        if(player != null)
            player.SwimState();
    }

    private void OnTriggerEnter(Collider other) {
        UFPlayerMovement player = other.GetComponent<UFPlayerMovement>();
        if(player != null)
            this.player = player;

    }

    private void OnTriggerExit(Collider other) {
        UFPlayerMovement player = other.GetComponent<UFPlayerMovement>();
        if(player != null)
            this.player = null;
    }
}