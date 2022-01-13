#if UNITY_EDITOR
using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NewGame;
using RoslynCSharp.Compiler;
using RoslynCSharp.HotReloading;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.Compilation;
using Assembly = System.Reflection.Assembly;

public class SnapshotInjectorEditor : OdinEditorWindow, IAssemblyProcessor
{
    [ShowInInspector]
    private SnapShotInjectorSettintScriptableObject _injectorSettingScriptableObject;


    public static SnapshotInjectorEditor Instance;

    [MenuItem("My Ui Commands/SnapshotInjectorEditor")]
    private static void ShowWindow()
    {
        Instance = GetWindow<SnapshotInjectorEditor>();
        RegisterToAssemblyProcessor(Instance);

        Instance.Show();
        if (Instance._injectorSettingScriptableObject == null)
        {
            Instance._injectorSettingScriptableObject = FindSnapShotSettingAsset();
        }
    }

    private static SnapShotInjectorSettintScriptableObject FindSnapShotSettingAsset()
    {
        // Try to find the asset
        string[] guids =
            AssetDatabase.FindAssets("t:" + typeof(SnapShotInjectorSettintScriptableObject).Name);

        if (guids.Length == 0)
        {
            Debug.LogWarningFormat("Failed to load settings asset '{0}'",
                typeof(SnapShotInjectorSettintScriptableObject));
            return null;
        }

        // Get the asset path
        string loadPath = AssetDatabase.GUIDToAssetPath(guids[0]);

        // Load the asset
        return AssetDatabase.LoadAssetAtPath<SnapShotInjectorSettintScriptableObject>(loadPath);
    }


    public static string AssemblyLocation;
    public static Assembly MainAssembly = typeof(testScript).Assembly;


    private static void FillAssemblyName()
    {
        AssemblyLocation = typeof(testScript).Assembly.Location;
    }

    [InitializeOnLoadMethod]
    private static void OnInitialized()
    {
        CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;

    }

    private static bool CompilerMessagesContainError(CompilerMessage[] messages)
    {
        return messages.Any(msg => msg.type == CompilerMessageType.Error);
    }


    private static void OnCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        // Do nothing if there were compile errors on the target
        if (CompilerMessagesContainError(messages))
        {
            Debug.Log(" stop because compile errors on target");
            return;
        }

        FillAssemblyName();
        // its kinda useless code for now its just for testing string comparison
        if (assemblyPath.Contains("-Editor") || assemblyPath.Contains(".Editor") ||
            assemblyPath.Contains("Editor.dll"))
        {
            Debug.Log("These are only editor dlls not the runtime dll");
            return;
        }

        var normalizeScriptPath = AssemblyLocation.Replace('\\', '/');
        var RelativePath = FileUtil.GetProjectRelativePath(normalizeScriptPath);


        if (assemblyPath.Contains(RelativePath))
        {
            InjectCode();
        }
    }

    [MenuItem("My Ui Commands/injectMethod #&q")]
    public static void InjectCode()
    {
        if (!FindSnapShotSettingAsset()._shouldEnableInject)
        {
            Debug.Log(" SnapShot injector is disabled");
            return;
        }

        var CustomAssemmblyResolver = new DefaultAssemblyResolver();
        var MainAssemblyDirectoryPath = Path.GetDirectoryName(MainAssembly.Location);
        CustomAssemmblyResolver.AddSearchDirectory(MainAssemblyDirectoryPath);

        if (AssemblyLocation == null) return;
        using (var AssemblyDefinitionInstance = AssemblyDefinition.ReadAssembly(AssemblyLocation,
            new ReaderParameters
            {
                ReadWrite = true, ReadSymbols = true,
                AssemblyResolver = CustomAssemmblyResolver
            }))
        {
            var TypeDefinitions = AssemblyDefinitionInstance.MainModule.GetTypes().Where(
                definition =>
                {
                    var att = definition.CustomAttributes;
                    foreach (var CustomAttribute in att)
                        if (CustomAttribute.AttributeType.Name ==
                            nameof(SnapShotAttributes.SnapShotInjectionAttribute))
                            return true;

                    return false;
                });

            var MethodsTOinject = new List<MethodDefinition>();

            foreach (var TypeDefinition in TypeDefinitions)
            {
                bool CheckforAttributes(MethodDefinition definition)
                {
                    var at = definition.CustomAttributes;
                    foreach (var CustomAttribute in at)
                        if (CustomAttribute.AttributeType.Name ==
                            nameof(SnapShotAttributes.IgnoreSnapShotInjectionAttribute))
                            return false;

                    return true;
                }


                var dd = TypeDefinition.Methods.ToList();
                foreach (var MethodDefinition in dd)
                {
                    var IsMethodEligible = CheckforAttributes(MethodDefinition);
                    if (!IsMethodEligible) continue;
                    MethodsTOinject.Add(MethodDefinition);
                }
            }


            TypeDefinition InjectMethodType = null;
            InjectMethodType =
                AssemblyDefinitionInstance.MainModule.GetType(typeof(SnapshotDebubber).ToString());

            MethodDefinition InstructionsOfMethodToinject;
            MethodDefinition TakeSnapshotForParametersMethod;

            if (InjectMethodType == null) // it means its this assembly is from Roslyn
            {
                #region I also Inject on roslyn assemblies and they don't have snapshotdebugger class reference which is necessary so I need to get that class from main assembly(Assembly-Csharp)

                var ImportOnEntry =
                    AssemblyDefinitionInstance.MainModule.ImportReference(
                        typeof(SnapshotDebubber).GetMethod("OnEntry"));
                var ImportParametersMethod =
                    AssemblyDefinitionInstance.MainModule.ImportReference(
                        typeof(SnapshotDebubber).GetMethod("TakeSnapshotForParameters"));


                InstructionsOfMethodToinject = ImportOnEntry.Resolve();

                TakeSnapshotForParametersMethod = ImportParametersMethod.Resolve();

                #endregion
            }
            else
            {
                InstructionsOfMethodToinject =
                    InjectMethodType.Methods.Single(definition => definition.Name == "OnEntry");
                TakeSnapshotForParametersMethod = InjectMethodType.Methods.Single(definition =>
                    definition.Name == "TakeSnapshotForParameters");
            }


            var InstructionsOfMethodToinjectList =
                InstructionsOfMethodToinject.Body.Instructions.ToList();
            InstructionsOfMethodToinjectList.Reverse();
            InstructionsOfMethodToinjectList.RemoveAt(0);

            if (MethodsTOinject.Count != 0)
            {
                var FilterMethodList = FilterMethodDefinitions(MethodsTOinject.ToList());

                foreach (var MethodDefinition in FilterMethodList)

                {
                    #region parameter il code

                    var arrayDef = new VariableDefinition(
                        new ArrayType(AssemblyDefinitionInstance.MainModule.TypeSystem
                            .Object)); // create variable to hold the array to be passed to the LogEntry() method    

                    MethodDefinition.Body.Variables
                        .Add(arrayDef); // add variable to the method          

                    var IlProcessor = MethodDefinition.Body.GetILProcessor();
                    var ParametersInstructions = new List<Instruction>();

                    ParametersInstructions.Add(IlProcessor.Create(OpCodes.Ldc_I4,
                        MethodDefinition.Parameters
                            .Count)); // load to the stack the number of parameters                      
                    ParametersInstructions.Add(IlProcessor.Create(OpCodes.Newarr,
                        AssemblyDefinitionInstance.MainModule.TypeSystem
                            .Object)); // create a new object[] with the number loaded to the stack           
                    ParametersInstructions.Add(IlProcessor.Create(OpCodes.Stloc,
                        arrayDef)); // store the array in the local variable

                    // loop through the parameters of the method to run
                    for (var i = 0; i < MethodDefinition.Parameters.Count; i++)
                    {
                        ParametersInstructions.Add(IlProcessor.Create(OpCodes.Ldloc,
                            arrayDef)); // load the array from the local variable
                        ParametersInstructions.Add(IlProcessor.Create(OpCodes.Ldc_I4,
                            i)); // load the index
                        ParametersInstructions.Add(IlProcessor.Create(OpCodes.Ldarg,
                            i + 1)); // load the argument of the original method (note that parameter 0 is 'this', that's omitted)

                        if (MethodDefinition.Parameters[i].ParameterType.IsValueType)
                            ParametersInstructions.Add(IlProcessor.Create(OpCodes.Box,
                                MethodDefinition.Parameters[i]
                                    .ParameterType)); // boxing is needed for value types
                        else
                            ParametersInstructions.Add(IlProcessor.Create(OpCodes.Castclass,
                                AssemblyDefinitionInstance.MainModule.TypeSystem
                                    .Object)); // casting for reference types

                        ParametersInstructions.Add(
                            IlProcessor.Create(OpCodes.Stelem_Ref)); // store in the array
                    }

                    #endregion

                    ParametersInstructions.Reverse();

                    var FinalInstructionsToinject = new List<Instruction>();

                    #region for calling parameter method

                    // beware of sequence of instruction  right now its correct
                    FinalInstructionsToinject.Add(IlProcessor.Create(OpCodes.Call,
                        TakeSnapshotForParametersMethod)); // call the LogEntry() method
                    FinalInstructionsToinject.Add(IlProcessor.Create(OpCodes.Ldloc,
                        arrayDef)); // load the array to the stack

                    #endregion

                    FinalInstructionsToinject.AddRange(ParametersInstructions);

                    //
                    FinalInstructionsToinject.AddRange(InstructionsOfMethodToinjectList);


                    foreach (var newInstruction in FinalInstructionsToinject
                    ) // add the new instructions in referse order
                    {
                        if (newInstruction.Operand is MethodReference Reference)

                        {
                            newInstruction.Operand =
                                AssemblyDefinitionInstance.MainModule.ImportReference(Reference);
                        }

                        var firstInstruction = MethodDefinition.Body.Instructions[0];

                        var processor = MethodDefinition.Body.GetILProcessor();
                        processor.InsertBefore(firstInstruction, newInstruction);
                    }
                }

                var writeParams = new WriterParameters {WriteSymbols = true};

                try
                {
                    AssemblyDefinitionInstance
                        .Write(writeParams); // Write to the same file that was used to open the file


                    Debug.Log(" finished injecting methods count==" + FilterMethodList.Count +
                              "   assemblyLocation is--" + AssemblyLocation);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    throw;
                }
            }
            else
            {
                Debug.Log("No methods to inject ");
            }
        }
    }


    public static List<MethodDefinition> FilterMethodDefinitions(
        List<MethodDefinition> rawMethodDefinitionsList)
    {
        var filteredList = new List<MethodDefinition>();

        foreach (var MethodDefinition in rawMethodDefinitionsList)
        {
            //if (MethodDefinition.IsSetter || MethodDefinition.IsGetter ||
            //    MethodDefinition.IsConstructor || MethodDefinition.IsSpecialName ||
            //    MethodDefinition.IsStatic )
            //    continue;

            if (MethodDefinition.IsConstructor || MethodDefinition.IsSpecialName ||
                MethodDefinition.IsStatic || MethodDefinition.Name=="Update")
                continue;

            filteredList.Add(MethodDefinition);
        }

        return filteredList;
    }


     static void RegisterToAssemblyProcessor(SnapshotInjectorEditor instance)
    {
        if (RealtimeScriptingService.domain.RoslynCompilerService.ContainsAssemblyProcessor(instance))
        {
            return;
        }
        RealtimeScriptingService.domain.RoslynCompilerService.AddAssemblyProcessor(instance);
    }

    public void OnProcessAssembly(AssemblyOutput assembly)
    {
        AssemblyLocation = assembly.AssemblyFilePath;
        InjectCode();
    }
}
#endif