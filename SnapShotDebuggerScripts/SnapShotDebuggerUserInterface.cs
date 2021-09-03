using UnityEngine;
using System.Collections;
using NewGame;
using Sirenix.OdinInspector;
using UnityEditor;

public class SnapShotDebuggerUserInterface : MonoBehaviour
{

    [SerializeField]int _indexOfFunctionToUndo=-1;

    void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

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

}
