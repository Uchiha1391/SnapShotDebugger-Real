#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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

        public static void TakeSnapshot(object MethodClassInstance,
            string MethodName,
            StackFrame[] StackFrameOfTheMethod)
        {
            if (!ShouldTakeSnapShot)
            {
                return;
            }


            var isbaseClassMonobehaviour =
                MethodClassInstance.GetType().CheckIfBaseClassMonobehaviourRecursive();
            SerializationContext serializationContext = new SerializationContext
            {
                Config = new SerializationConfig
                    { SerializationPolicy = SerializationPolicies.Everything }
            };
            if (isbaseClassMonobehaviour)
            {
                {
                    byte[] dataByteArray = null;
                    List<Object> ReferncedObjectsList = null;
                    UnitySerializationUtility.SerializeUnityObject((Object)MethodClassInstance,
                        ref dataByteArray, ref ReferncedObjectsList, DataFormat.JSON, true,
                        serializationContext);
                    var checkJson = Encoding.ASCII.GetString(dataByteArray);
                    Type MonoInterfaceOrAbstractClass = MethodClassInstance.GetType()
                        .GetInterfaces().FirstOrDefault();
                    if (MonoInterfaceOrAbstractClass == null)
                    {
                        if (MethodClassInstance.GetType().BaseType?.Name != "MonoBehaviour")
                        {
                            MonoInterfaceOrAbstractClass = MethodClassInstance.GetType().BaseType;
                        }
                    }

                    var casting = MethodClassInstance as MonoBehaviour;
                    bool isInvokedByMe = true;


                    #region to know which method has futher inkoved which method... to make a chain

                    if (MethodsRelatedData.Count != 0)
                    {
                        for (var index = 1; index < StackFrameOfTheMethod.Length; index++)
                        {
                            var t = StackFrameOfTheMethod[index];
                            var StackMethodName = t.GetMethod().Name;

                            var MethodsRelatedDatalength = MethodsRelatedData.Count;
                            for (int i = MethodsRelatedDatalength - 1; i >= 0; i--)
                            {
                                var snapshotStructure = MethodsRelatedData[i];
                                if (snapshotStructure.MethodName != StackMethodName)
                                {
                                    continue;
                                }

                                snapshotStructure.MethodInvokedByThisMethod.Add((MethodName,
                                    MethodsRelatedDatalength - i, MethodsRelatedData.Count - 1));
                                isInvokedByMe = false;

                                break;
                            }

                            if (isInvokedByMe)
                            {
                                break;
                            }
                        }
                    }

                    #endregion


                    var MethodClassInstanceGuid =
                        CreateMonobehaviourCreateInstanceGuid(MethodClassInstance);

                    var snapShotDataStructure = new SnapShotDataStructure(MethodName,
                        MethodClassInstanceGuid, parametersData: null, dataByteArray, ReferncedObjectsList,
                        StackFrameOfTheMethod, MonoInterfaceOrAbstractClass?.Name,
                        casting.gameObject, isInvokedByMe);
                    MethodsRelatedData.Add(snapShotDataStructure);
                }
            }
            else
            {
                var serializeValue = SerializationUtility.SerializeValue(MethodClassInstance,
                    DataFormat.JSON, serializationContext);
                var NonMonoInterface =
                    MethodClassInstance.GetType()
                        .GetInterface(nameof(SnapShotAttributes.HotReloadedInterfaceAttribute));
                bool isInvokedByMe = false;
                if (StackFrameOfTheMethod.Length > 2)
                {
                    var declaringType = StackFrameOfTheMethod[1].GetMethod().DeclaringType;
                    var ttt = declaringType != null && declaringType.Assembly.GetName()
                        .Name.Equals("Assembly-CSharp");
                    if (ttt == false)
                    {
                        isInvokedByMe = true;
                    }
                }

                var methodClassInstanceGuid =
                    InstancesTrackerAndCreator.GetGuidFromInstance(MethodClassInstance);

                var snapShotDataStructure = new SnapShotDataStructure(MethodName,
                    methodClassInstanceGuid, parametersData: null, dataBytesArray: serializeValue, null,
                    StackFrameOfTheMethod, NonMonoInterface?.Name, null, isInvokedByMe);
                MethodsRelatedData.Add(snapShotDataStructure);
            }


            Debug.Log("No. of snapshots taken--" + NumberOfSnapShotstaken);
            NumberOfSnapShotstaken++;
        }

        private static string CreateMonobehaviourCreateInstanceGuid(object MethodClassInstance)
        {
            string MethodClassInstanceGuid;
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            if (!InstancesTrackerAndCreator.MonobehaviourCachedReferences.Any(n =>
                    n.Value.Latestinstance.Equals(MethodClassInstance)))
            {
                MethodClassInstanceGuid = Guid.NewGuid().ToString();
                InstancesTrackerAndCreator.MonobehaviourCachedReferences.Add(
                    MethodClassInstanceGuid,
                    (assemblyPath, MethodClassInstance.GetType().FullName, MethodClassInstance));
            }
            else
            {
                MethodClassInstanceGuid =
                    InstancesTrackerAndCreator.MonobehaviourCachedReferences.FirstOrDefault(n=>
                    {
                        var Isequals = n.Value.Latestinstance.Equals(MethodClassInstance);
                        return Isequals;
                    }).Key;
            }

            return MethodClassInstanceGuid;
        }


        #region they are used for Il injectoin

        public void OnEntry()
        {
            SnapshotDebubber.TakeSnapshot(this, MethodBase.GetCurrentMethod().Name,
                new StackTrace(true).GetFrames());
        }

        public static void TakeSnapshotForParameters(object[] parameters)
        {
            if (!SnapshotDebubber.ShouldTakeSnapShot) return;
            var ValueTuple = SnapshotDebubber.MethodsRelatedData.Last();
            if (parameters.Length > 0)
            {
                var SerializationContext = new SerializationContext
                {
                    Config = new SerializationConfig
                        { SerializationPolicy = SerializationPolicies.Everything }
                };
                var memoryStream = new MemoryStream();
                List<Object> unityObjectsList;
                SerializationUtility.SerializeValue(parameters, memoryStream, DataFormat.JSON, out unityObjectsList,
                    SerializationContext);


                ValueTuple.ParametersData = (memoryStream.ToArray(), unityObjectsList);
            }
        }

        #endregion


        /// <summary>
        ///     actually its just for checking size. now i Now its too small so I shouldnt care
        /// </summary>
        public static void SizeofDebuggingData()
        {
            var dd = SerializationUtility.SerializeValue(MethodsRelatedData, DataFormat.JSON);
            var size = SizeConverterCustom.ToSize(dd.Length * sizeof(byte),
                SizeConverterCustom.SizeUnits.MB);
            EditorUtility.DisplayDialog("Size of debuuger list", size, "ok");
        }

        #endregion

        #region undo methods

        #region undo main method overloads

        public static bool UndoMainMethodSpecific(int index,
            bool skipSnapshot = false,
            bool skipOtherUndoTypes = false)
        {
            return UndoMethodInvokeInternal(index, skipSnapshot, skipOtherUndoTypes);
        }

        #endregion


        private static bool UndoMethodInvokeInternal(int index,
            bool skipSnapshot,
            bool skipOtherUndoTypes)
        {
            var methodToUndo = MethodsRelatedData[index];
            if (methodToUndo.IsUndoItself)
                return false; // only undo the methods that are not already undo
            if (!skipSnapshot)
            {
                UndoSnapshotMethod(methodToUndo);
            }

            if (!skipOtherUndoTypes)
            {
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
            }

            methodToUndo.IsUndoItself = true;
            return true;
        }

        private static void UndoSnapshotMethod(object datamethodToUndo)
        {
            var deserializationContext = new DeserializationContext
            {
                Config = new SerializationConfig
                    { SerializationPolicy = SerializationPolicies.Everything }
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
                UnitySerializationUtility.DeserializeUnityObject(
                    (Object)castedData.MethodClassInstance, ref castedDataDataBytesArray,
                    ref referencedUnityObjects, DataFormat.JSON, deserializationContext);
            }
            else
            {
                var ttt = SerializationUtility.DeserializeValue<object>(castedDataDataBytesArray,
                    DataFormat.JSON, deserializationContext);
                InstancesTrackerAndCreator.NonMonoCachedObjectReferences[
                        castedData.MethodClassInstanceGuid] =
                    (ttt.GetType().Assembly.Location, ttt.GetType().ToString(), ttt);
            }
        }

        private static void UndoPlaymodeSaveMethod(object methodToUndoData)
        {
            if (methodToUndoData is List<Component>)
            {
                List<Component> castedData = (List<Component>)methodToUndoData;
                foreach (var component in castedData)
                {
                    CustomPlayModeSerializer.LoadComponentData(component);
                }
            }
            else
            {
                Component castedData = (Component)methodToUndoData;
                CustomPlayModeSerializer.LoadComponentData(castedData);
            }
        }

        private static void UndoCustomMethod(string methodname,
            object methodToUndoData,
            object methodClassInstance)
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
                    SerializationUtility.DeserializeValue<object>((byte[])methodToUndoData,
                        DataFormat.JSON);
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

        public static void SaveCustomUndoData(string methodName,
            object CustomdataToSave,
            object methodClassInstance)
        {
            if (!ShouldTakeSnapShot)
            {
                Debug.LogError("snapshot taker is off.... turn it on");
                return;
            }

            var serializaeData =
                SerializationUtility.SerializeValue(CustomdataToSave, DataFormat.JSON);
            var snapShotDataStructureLast = MethodsRelatedData.Last();
            if (methodName == snapShotDataStructureLast.MethodName)
            {
                var undoDataInstance = new UndoDataClass(MethodUndoTypes.Custom, serializaeData);
                snapShotDataStructureLast.undoDataList.Add(undoDataInstance);
            }
            else
            {
                Debug.LogError(
                    " you havent put snapshot injector attribute .... so you cannot use undo system");
            }
        }

        public static void SaveComponents(string methodName, List<Component> componentsToSave)
        {
            if (!ShouldTakeSnapShot)
            {
                Debug.LogError("snapshot taker is off.... turn it on");
                return;
            }
            var DistinctList = componentsToSave.Distinct().ToList();


            foreach (var component in DistinctList)
            {
                CustomPlayModeSerializer.SaveComponent(component);
            }

            var snapShotDataStructureLast = MethodsRelatedData.Last();


            if (methodName == snapShotDataStructureLast.MethodName)
            {
                var undoDataInstance =
                    new UndoDataClass(MethodUndoTypes.playmodeSave, DistinctList);
                snapShotDataStructureLast.undoDataList.Add(undoDataInstance);
            }
            else
            {
                Debug.LogError(
                    " you havent put snapshot injector attribute .... so you cannot use undo system");
            }
        }

        public static void SaveComponent(string methodName, Component componentsToSave)
        {
            if (!ShouldTakeSnapShot)
            {
                Debug.LogError("snapshot taker is off.... turn it on");
                return;
            }

            CustomPlayModeSerializer.SaveComponent(componentsToSave);

            var snapShotDataStructureLast = MethodsRelatedData.Last();


            if (methodName == snapShotDataStructureLast.MethodName)
            {
                var undoDataInstance =
                    new UndoDataClass(MethodUndoTypes.playmodeSave, componentsToSave);
                snapShotDataStructureLast.undoDataList.Add(undoDataInstance);
            }
            else
            {
                Debug.LogError(
                    " you havent put snapshot injector attribute .... so you cannot use undo system");
            }
        }

        public static object[] deserializeParameters(object parametersData)
        {
            (Byte[] databytes, List<Object> unityObjectList) castedData = ((byte[], List<Object>))parametersData;


            var deserializationContext = new DeserializationContext
            {
                Config = new SerializationConfig
                    { SerializationPolicy = SerializationPolicies.Everything }
            };
            return SerializationUtility.DeserializeValue<object[]>(castedData.databytes, DataFormat.JSON,
                castedData.unityObjectList, deserializationContext);
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
        public MethodUndoTypes methodUndoTypes;
        public object data;

        public UndoDataClass(MethodUndoTypes methodUndoTypes, object data)
        {
            this.methodUndoTypes = methodUndoTypes;
            this.data = data;
        }
    }

    public class SnapShotDataStructure
    {
        #region Undo related

        public bool IsUndoItself;
        /// <summary>
        /// changes the value when method is invoked from the snapshot viewer
        /// </summary>
        public bool ChangeUndoStateOnIvoke;

        #endregion


        public string MethodName { get; set; }
        public GameObject InstanceGameObject;
        public string InstanceInterfaceOrAbstrctClassName;
        public string MethodClassInstanceGuid;

        public object MethodClassInstance
        {
            get
            {
                if (!string.IsNullOrEmpty(MethodClassInstanceGuid))
                {
                    var instace = InstancesTrackerAndCreator.GetInstanceFromGuid(MethodClassInstanceGuid);

                    return instace;
                }

                return null;
            }
        }

        public object ParametersData { get; set; }

        /// <summary>
        /// it can be serialization CustomdataToSave if its unity object or it can be byte[] if its non-unity object
        /// </summary>
        public byte[] DataBytesArray { get; set; }

        public List<Object> ReferncedObjectsList { get; set; }
        public StackFrame[] StackFrameOfTheMethod { get; set; }
        public bool IsEventBasedExecution;
        public List<UndoDataClass> undoDataList = new List<UndoDataClass>();

        public List<(string methodName, int indent, int index)> MethodInvokedByThisMethod =
            new List<(string methodName, int indent, int index)>();

        public SnapShotDataStructure(string methodName,
            string methodClassInstanceGuid,
            object parametersData,
            byte[] dataBytesArray,
            List<Object> referncedObjectsList,
            StackFrame[] stackFrameOfTheMethod,
            string instanceInterfaceOrAbstrctClassName,
            GameObject instanceGameObject,
            bool isEventBasedExecution)
        {
            MethodName = methodName;
            MethodClassInstanceGuid = methodClassInstanceGuid;
            ParametersData = parametersData;
            DataBytesArray = dataBytesArray;
            ReferncedObjectsList = referncedObjectsList;
            StackFrameOfTheMethod = stackFrameOfTheMethod;
            InstanceGameObject = instanceGameObject;
            InstanceInterfaceOrAbstrctClassName = instanceInterfaceOrAbstrctClassName;
            IsEventBasedExecution = isEventBasedExecution;
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