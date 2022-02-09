using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NewGame;
using RoslynCSharp.HotReloading;
using Sirenix.OdinInspector;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using static NewGame.SnapshotDebubber;


public class UndoEditorFunctionality : IUndoEditorFunctionality,IEditorWindowHotReload
{
    private UndoEditorFunctionality()
    {
    }

    private List<UndoDataClass> _undoMethodFilteredList =
        new List<UndoDataClass>();

    [ListDrawerSettings(OnBeginListElementGUI = "BeginDrawListElement",
        OnEndListElementGUI = "EndDrawListElement", Expanded = true)]
    [ShowInInspector]
    public List<SnapshotDebubber.UndoDataClass> UndoMethodFilteredList
    {
        get
        {
            RefreshUndoMethodFilteredList();
            return _undoMethodFilteredList;
        }
        set => _undoMethodFilteredList = value;
    }

    public void RefreshUndoMethodFilteredList()
    {
        _undoMethodFilteredList = NewMethodUndoRelatedList
            .Where(n => n.methodUndoTypes == MethodUndoTypes.Snapshot).ToList();
    }

    public void UndoTillThis(int index)
    {
        if (!NewMethodUndoRelatedList.Any())
        {
            Debug.Log("There is no method to undo");
            return;
        }

        var signatureFunction = UndoMethodFilteredList[index];

        var IndexInUndoRelatedMethodList = NewMethodUndoRelatedList.FindIndex(n =>
            n.hybridId == signatureFunction.hybridId &&
            signatureFunction.methodUndoTypes == MethodUndoTypes.Snapshot);
        for (int i = 0; i < NewMethodUndoRelatedList.Count - IndexInUndoRelatedMethodList; i++)
        {
            var methodToUndo = NewMethodUndoRelatedList[IndexInUndoRelatedMethodList + i];
            if (methodToUndo.IsUndo)
                continue;
            UndoMainMethodSpecific(methodToUndo);
        }
    }


    public void UndoThis(int index)
    {
        var methodUndo = UndoMethodFilteredList[index];
        if (methodUndo.IsUndo)
        {
            EditorUtility.DisplayDialog("Alert", "Already done Undo hahha", "Ok");
            return;
        }

        UndoMainMethodSpecific(methodUndo);
    }

    public void RedoTillThis(int index)
    {
        Debug.Log("Still not implemented");
    }


    public void RedoThis(int index)
    {
        ShouldTakeSnapShot = false;
        var signatureFunction = UndoMethodFilteredList[index];
        if (!signatureFunction.IsUndo)
        {
            EditorUtility.DisplayDialog("Alert", "First undo that function and then call redo",
                "Ok");
            return;
        }

        var snapShotFunction = NewMethodUndoRelatedList.FirstOrDefault(n =>
            n.hybridId == signatureFunction.hybridId &&
            signatureFunction.methodUndoTypes == MethodUndoTypes.Snapshot);
        var methodSnapshotData = (SnapShotDataStructure) snapShotFunction.data;
        var methodInfo = methodSnapshotData.MethodClassInstance.GetType().GetMethod(
            snapShotFunction.MethodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        methodInfo?.Invoke(methodSnapshotData.MethodClassInstance,
            (object[]) methodSnapshotData.ParametersData);
        ShouldTakeSnapShot = true;
    }


    private void BeginDrawListElement(int index)
    {
        SirenixEditorGUI.BeginBox("Actions");
        SirenixEditorGUI.BeginBox("Redo");

        if (SirenixEditorGUI.MenuButton(0, "RedoThis", true, null))
        {
            RedoThis(index);
            Debug.Log("Redo complete");

        }

        if (SirenixEditorGUI.MenuButton(0, "RedoTillThis", true, null))
        {
            RedoTillThis(index);
        }
        //
        SirenixEditorGUI.EndBox();

        SirenixEditorGUI.BeginBox("Undo");

        if (SirenixEditorGUI.MenuButton(20, "UndoThis", true, null))
        {
            UndoThis(index);
        }

        if (SirenixEditorGUI.MenuButton(20, "UndoTillThis", true, null))
        {
            UndoTillThis(index);
        }

        SirenixEditorGUI.EndBox();
    }

    private void EndDrawListElement(int index)
    {
        SirenixEditorGUI.EndBox();
    }

    public static IUndoEditorFunctionality GetInstanceFromGuid(string guid)
    {
        if (IUndoEditorFunctionality.ContainsKey(guid))
        {
            return (IUndoEditorFunctionality)IUndoEditorFunctionality[guid];
        }

        Debug.LogError("this guid is not registered ...");
        Debug.Break();
        return null;
    }

    public static string CreateInstanceAndItsGuid()
    {
        var tt = new UndoEditorFunctionality();
        var guid = Guid.NewGuid().ToString();
        IUndoEditorFunctionality.Add(guid, tt);
        return guid;
    }

    public static Dictionary<string, object> IUndoEditorFunctionality= new Dictionary<string, object>();
}