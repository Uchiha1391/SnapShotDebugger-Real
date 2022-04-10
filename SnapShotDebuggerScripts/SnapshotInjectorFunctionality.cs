using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using RoslynCSharp.Compiler;
using RoslynCSharp.HotReloading;

public class SnapshotInjectorFunctionality : IAssemblyProcessor,ISnapshotInjectorFunctionality
{
    private SnapshotInjectorFunctionality()
    {
    }

    public void OnProcessAssembly(AssemblyOutput assembly)
    {
        SnapshotInjectorEditor.AssemblyLocation = assembly.AssemblyFilePath;
        SnapshotInjectorEditor.InjectCode();

    }
    public void RegisterToAssemblyProcessor()
    {

        RealtimeScriptingService.domain.RoslynCompilerService.AddAssemblyProcessor(this);
        Debug.Log("snapshot injector registered to assembly processor");
    }
    

    public static ISnapshotInjectorFunctionality GetInstanceFromGuid(string guid)
    {
        if (ISnapshotInjectorFunctionality.ContainsKey(guid))
        {
            return (ISnapshotInjectorFunctionality)ISnapshotInjectorFunctionality[guid];
        }

        Debug.LogError("this guid is not registered ...");
        Debug.Break();
        return null;
    }

    public static string CreateInstanceAndItsGuid()
    {
        var tt = new SnapshotInjectorFunctionality();
        var guid = Guid.NewGuid().ToString();
        ISnapshotInjectorFunctionality.Add(guid, tt);
        return guid;
    }

    public static Dictionary<string, object> ISnapshotInjectorFunctionality= new Dictionary<string, object>();

}