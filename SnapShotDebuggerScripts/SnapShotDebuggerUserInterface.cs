using UnityEngine;
using System.Collections;
using NewGame;
using Sirenix.OdinInspector;
using UnityEditor;

public class SnapShotDebuggerUserInterface : MonoBehaviour
{

    public int IndexOfFunctionToUndo=-1;

    void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    [Button]
    public void LogAllMethods()
    {
        SnapshotDebubber.LogMethodds();
    }

    //
    [Button]
    public void UndoMethod()
    {
        if(IndexOfFunctionToUndo==-1)
        {
            EditorUtility.DisplayDialog("snapshot debugger Error", "set index before using","got it");
            return;
        }

        SnapshotDebubber.UndloadSnapShot(IndexOfFunctionToUndo);
        IndexOfFunctionToUndo = -1;
    }


    [Button]
    public void GetToCurrentExecution()
    {
        IndexOfFunctionToUndo = -1;

    }
    [Button]
    public void LogStackFrameOfIndexedMethod()
    {
        SnapshotDebubber.LogStackFrameOfGivenMethod(IndexOfFunctionToUndo);
        IndexOfFunctionToUndo = -1;

    }

    [Button]
    public void GetSizeAllocatedByDebuggeer()
    {
        SnapshotDebubber.SizeofDebuggingData();

    }

}
