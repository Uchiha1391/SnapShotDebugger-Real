using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ES3Editor;
using ES3Internal;
using Mono.CecilX;
using Mono.CecilX.Cil;
using Mono.CecilX.Rocks;
using NewGame;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Assets.Editor
{
    static class SnapshotInjector
    {
        private static string AssemblyLocationToAllow;
        
        public static Type TypeToGenerate = null;




        private static void FillAssemblyName()
        {
            AssemblyLocationToAllow = typeof(testScript).Assembly.Location;

        }
        [InitializeOnLoadMethod]
        private static void OnInitialized()
        {
            Debug.Log("Snapshot Injection Callback Initialized");

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
            }
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
                return;

            var normalizeScriptPath = AssemblyLocationToAllow.Replace('\\', '/');
            var RelativePath = FileUtil.GetProjectRelativePath(normalizeScriptPath);


            if (assemblyPath.Contains(RelativePath))
            {
                InjectCode();
            }
           
        }

        [MenuItem("My Ui Commands/injectMethod #&q")]
        public static void InjectCode()
        {
            var AssemblyFilePath = AssemblyLocationToAllow;
            using (var AssemblyDefinitionInstance = AssemblyDefinition.ReadAssembly(
                AssemblyFilePath, new ReaderParameters {ReadWrite = true, ReadSymbols = true}))
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

                    //
                    //MethodsTOinject = TypeDefinition.Methods.Where(CheckforAttributes).ToList();
                    var dd = TypeDefinition.Methods.ToList();
                    foreach (var MethodDefinition in dd)
                    {
                        var IsMethodEligible = CheckforAttributes(MethodDefinition);
                        if (!IsMethodEligible) continue;
                        MethodsTOinject.Add(MethodDefinition);
                    }
                }

                var InjectMethodType =
                    AssemblyDefinitionInstance.MainModule.GetType(
                        typeof(SnapshotDebubber).ToString());
                var InstructionsOfMethodToinjectEnurmator =
                    InjectMethodType.Methods.Single(definition => definition.Name == "OnEntry");
                var TakeSnapshotForParametersMethod = InjectMethodType.Methods.Single(definition =>
                    definition.Name == "TakeSnapshotForParameters");

                var InstructionsOfMethodToinjectList =
                    InstructionsOfMethodToinjectEnurmator.Body.Instructions.ToList();
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


                        foreach (var newInstruction in
                            FinalInstructionsToinject) // add the new instructions in referse order
                        {
                            var firstInstruction = MethodDefinition.Body.Instructions[0];
                            var processor = MethodDefinition.Body.GetILProcessor();
                            processor.InsertBefore(firstInstruction, newInstruction);
                        }

                        MethodDefinition.Body.Optimize();
                    }

                    var writeParams = new WriterParameters {WriteSymbols = true};

                    AssemblyDefinitionInstance
                        .Write(writeParams); // Write to the same file that was used to open the file


                    Debug.Log(" finished injecting methods count==" + FilterMethodList.Count);
                }
                else
                {
                    Debug.Log("No methods to inject ");
                }
            }

            //
        }

        public static List<MethodDefinition> FilterMethodDefinitions(
            List<MethodDefinition> rawMethodDefinitionsList)
        {
            var filteredList = new List<MethodDefinition>();

            foreach (var MethodDefinition in rawMethodDefinitionsList)
            {
                if (MethodDefinition.IsSetter || MethodDefinition.IsGetter ||
                    MethodDefinition.IsConstructor || MethodDefinition.IsSpecialName ||
                    MethodDefinition.IsStatic)
                    continue;

                filteredList.Add(MethodDefinition);
            }

            return filteredList;
        }

        [MenuItem("My Ui Commands/es3generate #&p")]
        public static void testingEs3Generate()
        {
            var TypeToCreateScriptFor = TypeToGenerate;

            CustomEs3TypeGeneratorData.Instance.fields =
                ES3Reflection.GetSerializableMembers(TypeToCreateScriptFor, false);

            CustomEs3TypeGeneratorData.Instance.fieldSelected =
                new bool[CustomEs3TypeGeneratorData.Instance.fields.Length];

            CustomEs3TypeGeneratorData.Instance
                .SelectAll(true,
                    true); // this code will automatically check for my class propeties and wont allow them to serialized

            //for (var Index = 0; Index < customEs3TypeGeneratorData.Instance.fieldSelected.Length; Index++)
            //{
            //    customEs3TypeGeneratorData.Instance.fieldSelected[Index] = true;
            //}

            CustomEs3TypeGeneratorData.Instance.GenerateCustom(TypeToCreateScriptFor);

            //customEs3TypeGeneratorData.Instance.SelectType(TypeToCreateScriptFor); // doesnt need this it servers no purpose
            Debug.Log("finished es3 type");
        }


    }
}