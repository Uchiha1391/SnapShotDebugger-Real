#if UNITY_EDITOR
using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Editor;
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


public class SnapshotInjector : IAssemblyProcessor, ISnapshotInjector
{
    private SnapshotInjector()
    {
        RegisterToAssemblyProcessor();
    }

    public static SnapshotInjector Instance { get; } = new SnapshotInjector();

    public enum UnityCSharpProjectFile
    {
        /// <summary>
        /// The main assembly where all runtime scripts are added, unless assembly definition files are used.
        /// </summary>
        Assembly_CSharp,

        /// <summary>
        /// The first pass assembly where all runtime scripts located inside the 'Plugins' folder are added, unless assembly definition files are used.
        /// </summary>
        Assembly_CSharp_Firstpass,

        /// <summary>
        /// The main assembly where all editor scripts located inside the 'Editor' folder are added, unless assembly definition files are used.
        /// </summary>
        Assembly_CSharp_Editor,

        /// <summary>
        /// The first pass assembly where all editor scripts located inside the 'Editor/Plugins' folder are added, unless assembly definition files are used.
        /// </summary>
        Assembly_CSharp_Editor_Firstpass,

        /// <summary>
        /// this is my snapshot debugger assembly
        /// </summary>
        SnapShotDebuggerAssembly
    }


    private static string AssemblyToInjectIn;

    private SnapShotInjectorSettintScriptableObject _InjectorSettingScriptableObject;

    public SnapShotInjectorSettintScriptableObject InjectorSettingScriptableObject
    {
        get
        {
            if (_InjectorSettingScriptableObject == null)
            {
                _InjectorSettingScriptableObject = AssetDatabase
                    .FindAssets($"t: {nameof(SnapShotInjectorSettintScriptableObject)}").ToList()
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<SnapShotInjectorSettintScriptableObject>)
                    .FirstOrDefault();
            }

            return _InjectorSettingScriptableObject;
        }

        set => _InjectorSettingScriptableObject = value;
    }


    public void InjectCode(string assemblyPath)
    {
        var CustomAssemmblyResolver = new DefaultAssemblyResolver();
        var SnapShotDebuggerAssemblyPath = Path.GetDirectoryName(typeof(SnapshotDebubber).Assembly.Location);
        CustomAssemmblyResolver.AddSearchDirectory(SnapShotDebuggerAssemblyPath);

        AssemblyToInjectIn = assemblyPath;
        using (var AssemblyDefinitionInstance = AssemblyDefinition.ReadAssembly(AssemblyToInjectIn,
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

                var writeParams = new WriterParameters { WriteSymbols = true };

                try
                {
                    AssemblyDefinitionInstance
                        .Write(writeParams); // Write to the same file that was used to open the file


                    Debug.Log(" finished injecting methods count==" + FilterMethodList.Count +
                              "   assemblyLocation is--" + AssemblyToInjectIn);
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
                MethodDefinition.IsStatic || MethodDefinition.Name == "Update")
                continue;

            filteredList.Add(MethodDefinition);
        }

        return filteredList;
    }

    public void OnProcessAssembly(AssemblyOutput assembly)
    {
        SnapshotInjector.Instance.InjectCode(assembly.AssemblyFilePath);
    }

    /// <summary>
    /// this is for registering Rosnlyn hot reloaded asseblies
    /// </summary>
    public void RegisterToAssemblyProcessor()
    {
        RealtimeScriptingService.domain.RoslynCompilerService.AddAssemblyProcessor(this);
        Debug.Log("snapshot injector registered to assembly processor");
    }
}
#endif