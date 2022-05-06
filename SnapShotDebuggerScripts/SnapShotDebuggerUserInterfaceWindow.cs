#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NewGame;
using RoslynCSharp.HotReloading;
using Sirenix.OdinInspector;
using UnityEditor;

public class SnapShotDebuggerUserInterface : OdinPracticeEditorWindow
{


    [MenuItem("My Ui Commands/SnapShotDebuggerUserInterface ")]
    public static void OpenWindow()
    {
        
        GetWindow<SnapShotDebuggerUserInterface>().Show();
    }

    [ShowInInspector]
    public bool SnapShotStart
    {
        get => SnapshotDebubber.ShouldTakeSnapShot;
        set => SnapshotDebubber.ShouldTakeSnapShot = value;
    }

    [ShowInInspector]
    public bool RestartHotReloading
    {
        get => RealtimeScriptWatcher.RestartHotReloading;
        set => RealtimeScriptWatcher.RestartHotReloading = value;
    }


    [Button]
    public void GetSizeAllocatedByDebuggeer()
    {
        SnapshotDebubber.SizeofDebuggingData();
    }

    [Button]
    public void UndoMethodlast()
    {
        SnapshotDebubber.UndoLastMethod();
        Debug.LogError("not implemnted right now");
    }

    [Button]
    public void OverwriteOrDeleteTempFile()
    {
        RealtimeScriptingService.watcher.OverwriteOrDeleteTempFile();
    }

}
#endif
