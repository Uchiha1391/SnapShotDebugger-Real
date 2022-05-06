using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MoreLinq;
using NewGame;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;



public class UndomethodEditorWindow : OdinEditorWindow 
{
    // private static string _guidUndoEditorFunctionalityInstance;
    //
    // [ShowInInspector]
    // private static IUndoEditorFunctionality UndoEditorFunctionalityInstance
    // {
    //     get
    //     {
    //         var ttt = UndoEditorFunctionality.GetInstanceFromGuid(
    //             _guidUndoEditorFunctionalityInstance);
    //        
    //         return ttt;
    //     }
    //     set
    //     {
    //         var instance =
    //             UndoEditorFunctionality.GetInstanceFromGuid(_guidUndoEditorFunctionalityInstance);
    //         instance = value;
    //     }
    // }
    //
    // [MenuItem("My Ui Commands/UndoMethodEditor")]
    // private static void ShowWindow()
    // {
    //     GetWindow<UndomethodEditorWindow>().Show();
    //     if (_guidUndoEditorFunctionalityInstance == null)
    //         _guidUndoEditorFunctionalityInstance =
    //              UndoEditorFunctionality.CreateInstanceAndItsGuid();
    // }
    //
}