using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public static class CustomPlayModeSerializer 
{
   static List<(int, SerializedObject)> customPlaymodeList=new List<(int,SerializedObject)>();
    public static void SaveComponent(Component component)
    {
        
        SerializedObject compSerObj = new SerializedObject(component);
        SerializedObject saveCompdata = new SerializedObject(component);
        var iterator = compSerObj.GetIterator();
        while (iterator.NextVisible(true))
        {
            saveCompdata.CopyFromSerializedProperty(iterator);
        }
        customPlaymodeList.Add((component.GetInstanceID(),saveCompdata));

    }

    public static void LoadComponentData(Component component)
    {
        SerializedObject compSerObj = new SerializedObject(component);
        SerializedObject saveCompdata = customPlaymodeList.FirstOrDefault(n=>n.Item1==component.GetInstanceID()).Item2;
        var iterator = saveCompdata.GetIterator();
        while (iterator.NextVisible(true))
        {
            compSerObj.CopyFromSerializedProperty(iterator);
        }
    }

}
