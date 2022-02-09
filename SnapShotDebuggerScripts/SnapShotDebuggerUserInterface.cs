using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NewGame;
using Sirenix.OdinInspector;

public class SnapShotDebuggerUserInterface : MonoBehaviour
{
#if UNITY_EDITOR

    [SerializeField]int _indexOfFunctionToUndo=-1;
    [SerializeField] private string _methodsJsonSerializedData;

    [ ShowInInspector]
    public bool SnapShotStart
    {
        get => SnapshotDebubber.ShouldTakeSnapShot;
        set => SnapshotDebubber.ShouldTakeSnapShot = value;
    }

    [Button]
    void setJsonMethodData(int index) => _methodsJsonSerializedData =
        SnapshotDebubber.MethodsRelatedData[index].DataJsonStringForReadability;


    [Button]
    public void LogAllMethods()
    {
        SnapshotDebubber.LogMethodds();
    }


    [Button]
    public void LogStackFrameOfIndexedMethod()
    {
        SnapshotDebubber.LogStackFrameOfGivenMethod(_indexOfFunctionToUndo);
        _indexOfFunctionToUndo = -1;

    }

    [Button]
    public void GetSizeAllocatedByDebuggeer()
    {
        SnapshotDebubber.SizeofDebuggingData();

    }
    [Button]
    public void UndoMethod()
    {
        SnapshotDebubber.UndoLastMethod();
        Debug.Log("undo method is called");
    }

#endif    
}

