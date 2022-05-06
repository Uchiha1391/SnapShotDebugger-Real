#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using ES3Internal;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace NewGame
{
    public class SnapshotDebubber
    {
        public static bool ShouldTakeSnapShot = false;
        private static int NumberOfSnapShotstaken { get; set; }


        public static List<SnapShotDataStructure> MethodsRelatedData { get; private set; } =
            new List<SnapShotDataStructure>();

        #region snashot related

        private static bool CheckIfBaseClassMonobehaviour(Type objectType)
        {
            Type baseTypeRecursive = objectType.BaseType;
            while (baseTypeRecursive != null)
            {
                if (baseTypeRecursive == typeof(MonoBehaviour))
                {
                    return true;
                }

                baseTypeRecursive = baseTypeRecursive.BaseType;
            }

            return false;
        }

        public static void TakeSnapshot(object MethodClassInstance, string MethodName,
            StackFrame[] StackFrameOfTheMethod)
        {
            if (!ShouldTakeSnapShot)
            {
                return;
            }

            var isbaseClassMonobehaviour = CheckIfBaseClassMonobehaviour(MethodClassInstance.GetType());
            SerializationContext serializationContext = new SerializationContext
            {
                Config = new SerializationConfig { SerializationPolicy = SerializationPolicies.Everything }
            };
            if (isbaseClassMonobehaviour)
            {
                {
                    byte[] dataByteArray = null;
                    List<Object> ReferncedObjectsList = null;
                    UnitySerializationUtility.SerializeUnityObject((Object)MethodClassInstance, ref dataByteArray,
                        ref ReferncedObjectsList, DataFormat.JSON, true, serializationContext);
                    var checkJson = Encoding.ASCII.GetString(dataByteArray);
                    Type MonoInterfaceOrAbstractClass = MethodClassInstance.GetType().GetInterfaces().FirstOrDefault();
                    if (MonoInterfaceOrAbstractClass == null)
                    {
                        if (MethodClassInstance.GetType().BaseType.Name != "MonoBehaviour")
                        {
                            MonoInterfaceOrAbstractClass = MethodClassInstance.GetType().BaseType;
                        }
                    }

                    var casting = MethodClassInstance as MonoBehaviour;
                    bool isInvokedByMe = true;


                    #region to know which method has futher inkoved which method... to make a chain

                    if (MethodsRelatedData.Count != 0)
                    {

                        if (StackFrameOfTheMethod.Length == 1)
                        {
                            isInvokedByMe = false;
                        }
                        else
                        {
                            var StackMethodName = StackFrameOfTheMethod[1].GetMethod().Name;

                            var MethodsRelatedDatalength = MethodsRelatedData.Count;
                            for (int i = MethodsRelatedDatalength - 1; i >= 0; i--)
                            {
                                var snapshotStructure = MethodsRelatedData[i];
                                if (snapshotStructure.MethodName != StackMethodName) continue;
                                snapshotStructure.MethodInvokedByThisMethod.Add((MethodName,
                                    MethodsRelatedDatalength - i,
                                    MethodsRelatedData.Count - 1));
                                isInvokedByMe = false;

                                break;
                            }
                        }
                    }

                    #endregion

                    // var ttt = MethodsRelatedData.Last().MethodName == StackFrameOfTheMethod[1].GetMethod().Name;
                    // if (!ttt)
                    // {
                    //     isInvokedByMe = true;
                    // }

                    var snapShotDataStructure = new SnapShotDataStructure(MethodName, MethodClassInstance, null,
                        dataByteArray, checkJson, ReferncedObjectsList, StackFrameOfTheMethod,
                        MonoInterfaceOrAbstractClass?.Name, casting.gameObject, isInvokedByMe);
                    MethodsRelatedData.Add(snapShotDataStructure);
                }
            }
            else
            {
                var serializeValue = SerializationUtility.SerializeValue(MethodClassInstance,
                    DataFormat.JSON, serializationContext);
                var checkJson = Encoding.ASCII.GetString(serializeValue);
                var NonMonoInterface = MethodClassInstance.GetType().GetInterfaces().FirstOrDefault();
                bool isInvokedByMe = false;
                if (StackFrameOfTheMethod.Length > 2)
                {
                    var ttt = StackFrameOfTheMethod[1].GetMethod().DeclaringType.Assembly.GetName().Name
                        .Equals("Assembly-CSharp");
                    if (ttt == false)
                    {
                        isInvokedByMe = true;
                    }
                }

                var snapShotDataStructure = new SnapShotDataStructure(MethodName, MethodClassInstance, null,
                    dataBytesArray: serializeValue, checkJson, null, StackFrameOfTheMethod, NonMonoInterface.Name, null,
                    isInvokedByMe);
                MethodsRelatedData.Add(snapShotDataStructure);
            }

            #region saving gameobjects

            //var (newSetting, newKey) = NewKeyAndEs3SettingGenerator();
            //AutosaveKeysAndSettinglist.Add((newKey, newSetting));
            //ES3AutoSaveMgr._current.Save(newKey, newSetting);

            #endregion

            ConsoleProDebug.Watch("No. of snapshots taken", NumberOfSnapShotstaken.ToString());
            NumberOfSnapShotstaken++;

            //catch (Exception e)
            //{
            //    // Get stack trace for the exception with source file information
            //    var st = new StackTrace(e, true);
            //    // Get the top stack frame
            //    var frame = st.GetFrame(0);
            //    // Get the line number from the stack frame
            //    var line = frame.GetFileLineNumber();

            //    Debug.LogError(e + "-" + line);

            //    throw;
            //}
        }

        private static ( ES3SerializableSettings newSetting, string newKey) NewKeyAndEs3SettingGenerator()
        {
            var newpath = "es3saver" + Guid.NewGuid() + ".es3";
            var newSetting = new ES3SerializableSettings(newpath);
            var newKey = Guid.NewGuid().ToString();
            return (newSetting, newKey);
        }

        [Obsolete("now its not used and is commented only")]
        public static void UndloadSnapShot(int indexOfMethod)
        {
            //if (indexOfMethod > MethodsRelatedData.Count)
            //{
            //    Debug.LogWarning("the index is bigger than the count of methods list");
            //    return;
            //}

            //CurrentUndoMethodIndex = MethodsRelatedData.Count - indexOfMethod;

            //for (var i = 0; i < CurrentUndoMethodIndex; i++)
            //{
            //    #region loading gameobjects original values

            //    if (AutosaveKeysAndSettinglist.Count > 0)
            //    {
            //        var (key, Es3SerializableSettings) =
            //            AutosaveKeysAndSettinglist[AutosaveKeysAndSettinglist.Count - i - 1];
            //        ES3AutoSaveMgr._current.Load(key, Es3SerializableSettings);
            //    }

            //    #endregion

            //    #region method class fields CustomdataToSave restore

            //    if (MethodsRelatedData.Count > 0)
            //    {
            //        var MethoDataStructure = MethodsRelatedData[MethodsRelatedData.Count - i - 1];

            //        if (MethoDataStructure.MethodClassInstance != null)
            //        {
            //            var DataBytesArray = MethoDataStructure.DataBytesArray;
            //            var checkJson = Encoding.ASCII.GetString(DataBytesArray.SerializedBytes);
            //            //UnitySerializationUtility.DeserializeUnityObject((Object) Instance, ref OdinData);
            //            UnitySerializationUtility.DeserializeUnityObject(
            //                (Object) MethoDataStructure.MethodClassInstance, ref DataBytesArray,
            //                new DeserializationContext
            //                {
            //                    Config = new SerializationConfig
            //                    {
            //                        SerializationPolicy = SerializationPolicies.Everything
            //                    }
            //                });
            //        }

            //        #region invoke the indexed method

            //        //if (i == CurrentUndoMethodIndex - 1)
            //        //{
            //        //    var Type = MethoDataStructure.MethodClassInstance.GetType();
            //        //    var MethodInfo = Type.GetMethod(MethodName,
            //        //        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            //        //    var FixedParameterdata = new List<object>();

            //        //    #region fixing parameters and fetching gameobjects references from es3 references

            //        //    if (ParametersData != null)
            //        //        foreach (var CurrentData in ParametersData)
            //        //            if (CurrentData is long) // I never use long so its ok. tag:#RiskyCode
            //        //            {
            //        //                var gameobject = ES3ReferenceMgrBase.Current.Get((long) CurrentData);
            //        //                FixedParameterdata.Add(gameobject);
            //        //            }
            //        //            else
            //        //            {
            //        //                FixedParameterdata.Add(CurrentData);
            //        //            }

            //        //    #endregion

            //        //    MethodInfo?.Invoke(Instance, FixedParameterdata.ToArray());
            //        //    //GetToCurrentState();
            //        //}

            //        #endregion
            //    }

            //    #endregion
            //}

            //EditorUtility.DisplayDialog("snapshot debugger",
            //    "undo is activated so no snapshot taken", "ok");
        }

        #region they are used for Il injectoin

        public void OnEntry()
        {
            TakeSnapshot(this, MethodBase.GetCurrentMethod().Name, new StackTrace(true).GetFrames());
        }

        public static void TakeSnapshotForParameters(object[] parameters)
        {
            if (!ShouldTakeSnapShot) return;
            var ValueTuple = MethodsRelatedData.Last();
            if (parameters.Length > 0)
            {
                var ParametersWithUnityObjectsReferences =
                    new List<object>(); //if gameobject gets destroyed then es3 refrence will help as es3 creates a new gameobject if existing is not present.
                foreach (var Parameter in parameters)
                    if (Parameter != null && Parameter.GetType() == typeof(Object))
                    {
                        var reference = ES3ReferenceMgrBase.Current.Get((Object)Parameter);
                        ParametersWithUnityObjectsReferences.Add(reference);
                    }
                    else
                    {
                        ParametersWithUnityObjectsReferences.Add(Parameter);
                    }

                ValueTuple.ParametersData = ParametersWithUnityObjectsReferences.ToArray();
            }

            var methodInfo = ValueTuple.MethodClassInstance.GetType().GetMethod(ValueTuple.MethodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        #endregion

        // static bool CheckForUndoAttributes(MethodInfo definition)
        // {
        //     var at = definition.CustomAttributes;
        //     foreach (var CustomAttribute in at)
        //         if (CustomAttribute.AttributeType.Name == nameof(SnapShotAttributes.UndoInjectionAttribute))
        //             return true;
        //     return false;
        // }

        public static void LogMethodds()
        {
            for (var Index = 0; Index < MethodsRelatedData.Count; Index++)
            {
                var ValueTuple = MethodsRelatedData[Index];
                ConsoleProDebug.LogToFilter(
                    ValueTuple.MethodClassInstance + ":" + ValueTuple.MethodName + "--Index=" + Index +
                    "--IsInovkedByMe=" + ValueTuple.IsEventBasedExecution, "SnapShotMethods");
            }
        }

        public static void LogStackFrameOfGivenMethod(int IndexOFMethod)
        {
            var ValueTuple = MethodsRelatedData[IndexOFMethod];
            foreach (var StackFrame in ValueTuple.StackFrameOfTheMethod)
                ConsoleProDebug.LogToFilter(StackFrame.ToString(), "StackframeOfGivenMethod");
        }

        /// <summary>
        ///     actually its just for checking size. now i Now its too small so I shouldnt care
        /// </summary>
        public static void SizeofDebuggingData()
        {
            var dd = SerializationUtility.SerializeValue(MethodsRelatedData, DataFormat.JSON);
            var json = Encoding.ASCII.GetString(dd);
            var size = SizeConverterCustom.ToSize(dd.Length * sizeof(byte), SizeConverterCustom.SizeUnits.MB);
            EditorUtility.DisplayDialog("Size of debuuger list", size, "ok");
        }

        #endregion

        #region undo methods

        #region undo main method overloads

        [Obsolete("now its not used and is commented only")]
        public static void UndoLastMethod()
        {
            // if (!NewMethodUndoRelatedList.Any())
            // {
            //     Debug.Log("There is no method to undo");
            //     return;
            // }
            //
            // var methodToUndo = NewMethodUndoRelatedList.Last();
            // UndoMainMethodSpecific(methodToUndo);
        }

        public static void UndoMainMethodSpecific(int index)
        {
            UndoMethodInvokeInternal(index);
        }

        #endregion

        private static void UndoMethodInvokeInternal(int index)
        {
            var methodToUndo = MethodsRelatedData[index];
            UndoSnapshotMethod(methodToUndo);

            foreach (var undoDataInstance in methodToUndo.undoDataList)
            {
                switch (undoDataInstance.methodUndoTypes)
                {
                    case MethodUndoTypes.playmodeSave:
                        UndoPlaymodeSaveMethod(undoDataInstance.data);
                        break;
                    case MethodUndoTypes.Custom:
                        UndoCustomMethod(methodToUndo.MethodName, undoDataInstance.data,
                            methodToUndo.MethodClassInstance);
                        break;
                    default: throw new ArgumentOutOfRangeException();
                }
            }

            methodToUndo.IsUndoItself = true;
        }

        private static void UndoSnapshotMethod(object datamethodToUndo)
        {
            var deserializationContext = new DeserializationContext
            {
                Config = new SerializationConfig { SerializationPolicy = SerializationPolicies.Everything }
            };
            var castedData = (SnapShotDataStructure)datamethodToUndo;
            var castedDataDataBytesArray = castedData.DataBytesArray;
            if (castedData.MethodClassInstance == null)
            {
                Debug.LogError(
                    " Method class instance is null meaning either its hot reloaded or destroyed. so undo is not possible");
                return;
            }

            if (castedData.ReferncedObjectsList != null)
            {
                var referencedUnityObjects = castedData.ReferncedObjectsList;
                UnitySerializationUtility.DeserializeUnityObject((Object)castedData.MethodClassInstance,
                    ref castedDataDataBytesArray, ref referencedUnityObjects, DataFormat.JSON, deserializationContext);
            }
            else
            {
                castedData.MethodClassInstance = SerializationUtility.DeserializeValue<object>(castedDataDataBytesArray,
                    DataFormat.JSON, deserializationContext);
            }
        }

        private static void UndoPlaymodeSaveMethod(object methodToUndoData)
        {
            var castedData = (List<Component>)methodToUndoData;
            foreach (var component in castedData)
            {
                CustomPlayModeSerializer.LoadComponentData(component);
            }
        }

        private static void UndoCustomMethod(string methodname, object methodToUndoData, object methodClassInstance)
        {
            var methodInfo = methodClassInstance.GetType().GetMethod("Undo" + methodname,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (methodInfo == null)
            {
                Debug.Log("Couldnt find the correspoding undo method");
                return;
            }

            if (methodToUndoData != null)
            {
                var parameterDataDeserializeValue =
                    SerializationUtility.DeserializeValue<object>((byte[])methodToUndoData, DataFormat.JSON);
                object[] parameters = new[] { parameterDataDeserializeValue };
                methodInfo.Invoke(methodClassInstance, parameters);
            }
            else
            {
                methodInfo.Invoke(methodClassInstance, null);
            }
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            NumberOfSnapShotstaken = 0;
            MethodsRelatedData.Clear();
            Debug.Log("snapshot debugger reset.");
        }

        public static void SaveCustomUndoData(string methodName, object CustomdataToSave, object methodClassInstance)
        {
            if (!ShouldTakeSnapShot)
            {
                Debug.LogError("snapshot taker is off.... turn it on");
                return;
            }

            var serializaeData = SerializationUtility.SerializeValue(CustomdataToSave, DataFormat.JSON);
            var snapShotDataStructureLast = MethodsRelatedData.Last();
            if (methodName == snapShotDataStructureLast.MethodName)
            {
                var undoDataInstance = new UndoDataClass(MethodUndoTypes.Custom, serializaeData);
                snapShotDataStructureLast.undoDataList.Add(undoDataInstance);
            }
            else
            {
                Debug.LogError(" you havent put snapshot injector attribute .... so you cannot use undo system");
            }
        }

        public static void SaveComponents(string methodName, List<Component> componentsToSave)
        {
            if (!ShouldTakeSnapShot)
            {
                Debug.LogError("snapshot taker is off.... turn it on");
                return;
            }

            foreach (var component in componentsToSave)
            {
                CustomPlayModeSerializer.SaveComponent(component);
            }

            var snapShotDataStructureLast = MethodsRelatedData.Last();


            if (methodName == snapShotDataStructureLast.MethodName)
            {
                var undoDataInstance = new UndoDataClass(MethodUndoTypes.playmodeSave, componentsToSave);
                snapShotDataStructureLast.undoDataList.Add(undoDataInstance);
            }
            else
            {
                Debug.LogError(" you havent put snapshot injector attribute .... so you cannot use undo system");
            }
        }
    }


    public enum MethodUndoTypes
    {
        Snapshot,
        playmodeSave,
        Custom
    }

    public class UndoDataClass
    {
        public string MethodName;
        public MethodUndoTypes methodUndoTypes;
        public object data;

        public UndoDataClass(MethodUndoTypes methodUndoTypes, object data
        )
        {
            this.methodUndoTypes = methodUndoTypes;
            this.data = data;
        }
    }

    public class SnapShotDataStructure
    {
        public bool IsUndoItself;
        public bool IsUndoItChain;

        public string MethodName { get; set; }
        public GameObject InstanceGameObject;
        public string InstanceInterfaceOrAbstrctClassName;
        public object MethodClassInstance { get; set; }
        public object ParametersData { get; set; }

        /// <summary>
        /// it can be serialization CustomdataToSave if its unity object or it can be byte[] if its non-unity object
        /// </summary>
        public byte[] DataBytesArray { get; set; }

        public List<Object> ReferncedObjectsList { get; set; }
        public StackFrame[] StackFrameOfTheMethod { get; set; }
        public bool IsEventBasedExecution;
        public string DataJsonStringForReadability;
        public List<UndoDataClass> undoDataList = new List<UndoDataClass>();

        public List<(string methodName, int indent, int index)> MethodInvokedByThisMethod =
            new List<(string methodName, int indent, int index)>();

        public SnapShotDataStructure(string methodName, object methodClassInstance, object parametersData,
            byte[] dataBytesArray, string dataJsonStringForReadability, List<Object> referncedObjectsList,
            StackFrame[] stackFrameOfTheMethod, string instanceInterfaceOrAbstrctClassName,
            GameObject instanceGameObject, bool isEventBasedExecution)
        {
            MethodName = methodName;
            MethodClassInstance = methodClassInstance;
            ParametersData = parametersData;
            DataBytesArray = dataBytesArray;
            ReferncedObjectsList = referncedObjectsList;
            StackFrameOfTheMethod = stackFrameOfTheMethod;
            InstanceGameObject = instanceGameObject;
            InstanceInterfaceOrAbstrctClassName = instanceInterfaceOrAbstrctClassName;
            IsEventBasedExecution = isEventBasedExecution;
            DataJsonStringForReadability = dataJsonStringForReadability;
        }
    }

    public static class SizeConverterCustom
    {
        public enum SizeUnits
        {
            Byte,
            KB,
            MB,
            GB,
            TB,
            PB,
            EB,
            ZB,
            YB
        }

        public static string ToSize(this long value, SizeUnits unit)
        {
            return (value / Math.Pow(1024, (long)unit)).ToString("0.00");
        }
    }
}
#endif