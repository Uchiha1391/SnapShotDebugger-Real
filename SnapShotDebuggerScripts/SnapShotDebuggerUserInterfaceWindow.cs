#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using NewGame;
using RoslynCSharp;
using RoslynCSharp.HotReloading;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEngine;

namespace SnapShotDebugger_Real.SnapShotDebugger.SnapShotDebugger_Real.SnapShotDebuggerScripts
{
    public class SnapShotDebuggerUserInterfaceWindow : OdinEditorWindow
    {
        [PropertySpace(20), ShowInInspector, PropertyOrder(50)]
        public List<string> RoslynComipileNewCreatedScripts
        {
            get => RealtimeScriptingService.watcher.NewCreatedScriptsToCompileWithRoslyn;
            set => RealtimeScriptingService.watcher.NewCreatedScriptsToCompileWithRoslyn = value;
        }

        #region Il injection related fields


        [PropertySpace(20), ShowInInspector, PropertyOrder(49), InlineEditor(InlineEditorObjectFieldModes.Foldout)]
        public SnapShotInjectorSettintScriptableObject InjectorSettingScriptableObject
        {
            get
            {
                return SnapshotInjector.Instance.InjectorSettingScriptableObject;
            }

            set => SnapshotInjector.Instance.InjectorSettingScriptableObject = value;
        }

        #endregion
        
        [InitializeOnLoadMethod]
        private static void OnInitialized()
        {
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
        }


        [MenuItem("My Ui Commands/SnapShotDebuggerUserInterfaceWindow ")]
        public static void OpenWindow()
        {
            GetWindow<SnapShotDebuggerUserInterfaceWindow>().Show();
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


        [Button, PropertyOrder(51)]
        public void Compile()
        {
            RealtimeScriptingService.watcher.CompileNewlyCreatedScriptsWithRoslyn();
        }


        [Button]
        public void GetSizeAllocatedByDebuggeer()
        {
            SnapshotDebubber.SizeofDebuggingData();
        }


        [Button]
        public void OverwriteOrDeleteTempFile()
        {
            RealtimeScriptingService.watcher.OverwriteOrDeleteTempFile();
        }

        #region Il injection

        
       
        private static bool CompilerMessagesContainError(CompilerMessage[] messages)
        {
            return messages.Any(msg => msg.type == CompilerMessageType.Error);
        }

        private static void OnCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            
            
            if (!SnapshotInjector.Instance.InjectorSettingScriptableObject._shouldEnableInject)
            {
                Debug.Log(" SnapShot injector is disabled");
                return;
            }
            // Do nothing if there were compile errors on the target
            if (CompilerMessagesContainError(messages))
            {
                Debug.Log(" stop because compile errors on target");
                return;
            }

            // its kinda useless code for now its just for testing string comparison
            if (assemblyPath.Contains("-Editor") || assemblyPath.Contains(".Editor") ||
                assemblyPath.Contains("Editor.dll"))
            {
                Debug.Log("These are only editor dlls not the runtime dll");
                return;
            }


            var assembly = CompilationPipeline.GetAssemblies().FirstOrDefault(a => a.outputPath == assemblyPath);
            if (assembly != null && assembly.flags == AssemblyFlags.EditorAssembly)
            {
                return;
            }


            SnapshotInjector.Instance.InjectCode(assemblyPath);
        }

        #endregion
    }
}
#endif