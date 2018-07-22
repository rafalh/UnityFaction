﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class TableReader{

    public enum TableType {
        clutter, items
    }

    private static Dictionary<TableType, string[]> tables;

    private const string tableExt = ".tbl";

    public static string FindClutterModel(string name) {
        return FindValue(TableType.clutter, "$V3D Filename", name);
    }

    public static string FindItemModel(string name) {
        return FindValue(TableType.items, "$V3D Filename", name);
    }

    //TODO make particle system from data in vclip.tbl

    private static string FindValue(TableType tableType, string property, string name) {
        string[] table = GetTable(tableType);

        for(int i = 0; i < table.Length; i++) {
            string line = table[i];
            bool header = line.Contains("Class Name:") && line.Contains(name);
            if(header) {
                while(!line.StartsWith(property))
                    line = table[++i];
                string quotedFileName = Regex.Match(line, "\"([^\"]*)\"").Value;
                int qfnLength = quotedFileName.Length;
                return quotedFileName.Substring(1, qfnLength - 6);
            }
        }

        Debug.LogWarning("Could not find " + name + " in Item table");
        return name;
    }

    private static string[] GetTable(TableType type) {
        if(tables == null)
            tables = new Dictionary<TableType, string[]>();
        if(tables.ContainsKey(type))
            return tables[type];

        string tableName = type.ToString();
        string fileName = tableName + tableExt;
        
        string[] results = AssetDatabase.FindAssets(tableName);

        string tblPath = null;
        foreach(string result in results) {
            string resultPath = AssetDatabase.GUIDToAssetPath(result);
            string resultName = Path.GetFileName(resultPath);
            if(resultName == fileName)
                tblPath = resultPath;
        }

        if(tblPath == null) {
            Debug.LogError("Could not find table file: " + fileName);
            return new string[0];
        }

        string[] lines = System.IO.File.ReadAllLines(tblPath);
        tables[type] = lines;

        return lines;
    }
}