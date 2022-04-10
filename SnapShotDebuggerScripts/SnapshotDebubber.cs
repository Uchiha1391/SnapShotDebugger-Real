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
    public enum MethodUndoTypes
    {
        Snapshot,
        playmodeSave,
        Custom
    }

    public class SnapshotDebubber
    {
        public static bool ShouldTakeSnapShot = false;
        private static int NumberOfSnapShotstaken { get; set; }
        public static List<UndoDataClass> NewMethodUndoRelatedList = new List<UndoDataClass>();

        public class UndoDataClass
        {
            public string MethodName;
            public MethodUndoTypes methodUndoTypes;
            public Guid hybridId;
            public object data;
            public bool IsUndo;

            public UndoDataClass(string methodName, MethodUndoTypes methodUndoTypes, Guid hybridId, object data,
                bool isUndo)
            {
                MethodName = methodName;
                this.methodUndoTypes = methodUndoTypes;
                this.hybridId = hybridId;
                this.data = data;
                IsUndo = isUndo;
            }
        }

        private static int _hybridUndoSlotCounter;
        private static Guid _currentHybridUndoID;

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
            if (!ShouldTakeSnapShot) return;
            var isbaseClassMonobehaviour = CheckIfBaseClassMonobehaviour(MethodClassInstance.GetType());
            SerializationContext serializationContext = new SerializationContext
            {
                Config = new SerializationConfig {SerializationPolicy = SerializationPolicies.Everything}
            };
            if (isbaseClassMonobehaviour)
            {
                {
                    byte[] dataByteArray = null;
                    List<Object> ReferncedObjectsList = null;
                    UnitySerializationUtility.SerializeUnityObject((Object) MethodClassInstance, ref dataByteArray,
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

            //    #region method class fields data restore

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
                        var reference = ES3ReferenceMgrBase.Current.Get((Object) Parameter);
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
            if (!CheckForUndoAttributes(methodInfo))
            {
                return;
            }

            if (_hybridUndoSlotCounter != 0)
            {
                NewMethodUndoRelatedList.Add(new UndoDataClass(ValueTuple.MethodName, MethodUndoTypes.Snapshot,
                    _currentHybridUndoID, ValueTuple, false));
                _hybridUndoSlotCounter--;
            }
            else
            {
                NewMethodUndoRelatedList.Add(new UndoDataClass(ValueTuple.MethodName, MethodUndoTypes.Snapshot,
                    Guid.Empty, ValueTuple, false));
            }
        }

        #endregion

        static bool CheckForUndoAttributes(MethodInfo definition)
        {
            var at = definition.CustomAttributes;
            foreach (var CustomAttribute in at)
                if (CustomAttribute.AttributeType.Name == nameof(SnapShotAttributes.UndoInjectionAttribute))
                    return true;
            return false;
        }

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

        public static void UndoLastMethod()
        {
            if (!NewMethodUndoRelatedList.Any())
            {
                Debug.Log("There is no method to undo");
                return;
            }

            var methodToUndo = NewMethodUndoRelatedList.Last();
            UndoMainMethodSpecific(methodToUndo);
        }

        public static void UndoMainMethodSpecific(UndoDataClass methodToUndo)
        {
            var nextMethodIndex =
                NewMethodUndoRelatedList.IndexOf(methodToUndo) +
                1; //because it will give the snapshot method type of hybrid methods undo
            UndoMethodInvokeInternal(methodToUndo);
            var previousMethodUndo = methodToUndo;
            while (true)
            {
                if (!NewMethodUndoRelatedList.Any()) return;
                if (nextMethodIndex >= NewMethodUndoRelatedList.Count) return;
                var methodToUndoNext = NewMethodUndoRelatedList[nextMethodIndex];
                if (!methodToUndoNext.hybridId.Equals(previousMethodUndo.hybridId))
                {
                    if (previousMethodUndo.methodUndoTypes != MethodUndoTypes.Snapshot)
                    {
                        break;
                    }
                }

                UndoMethodInvokeInternal(methodToUndoNext);
                previousMethodUndo = methodToUndoNext;
                nextMethodIndex++;
            }
        }

        #endregion

        private static void UndoMethodInvokeInternal(UndoDataClass methodToUndo)
        {
            switch (methodToUndo.methodUndoTypes)
            {
                case MethodUndoTypes.Snapshot:
                    UndoSnapshotMethod(methodToUndo.data);
                    break;
                case MethodUndoTypes.playmodeSave:
                    UndoPlaymodeSaveMethod(methodToUndo.data);
                    break;
                case MethodUndoTypes.Custom:
                    UndoCustomMethod(methodToUndo.MethodName, methodToUndo.data);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }

            methodToUndo.IsUndo = true;
        }

        private static void UndoSnapshotMethod(object datamethodToUndo)
        {
            var deserializationContext = new DeserializationContext
            {
                Config = new SerializationConfig {SerializationPolicy = SerializationPolicies.Everything}
            };
            var castedData = (SnapShotDataStructure) datamethodToUndo;
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
                UnitySerializationUtility.DeserializeUnityObject((Object) castedData.MethodClassInstance,
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
            var castedData = (List<Component>) methodToUndoData;
            foreach (var component in castedData)
            {
                CustomPlayModeSerializer.LoadComponentData(component);
            }
        }

        private static void UndoCustomMethod(string methodname, object methodToUndoData)
        {
            var castedData = ((object methodClassInstance, byte[] data)) methodToUndoData;
            var methodInfo = castedData.methodClassInstance.GetType().GetMethod("Undo" + methodname,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (methodInfo == null)
            {
                Debug.Log("Couldnt find the correspoding undo method");
                return;
            }

            if (castedData.data != null)
            {
                var parameterDataDeserializeValue =
                    SerializationUtility.DeserializeValue<object>(castedData.data, DataFormat.JSON);
                object[] parameters = new[] {parameterDataDeserializeValue};
                methodInfo.Invoke(castedData.methodClassInstance, parameters);
            }
            else
            {
                methodInfo.Invoke(castedData.methodClassInstance, null);
            }
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            NumberOfSnapShotstaken = 0;
            NewMethodUndoRelatedList.Clear();
            _hybridUndoSlotCounter = 0;
            _currentHybridUndoID = default;
            MethodsRelatedData.Clear();
            Debug.Log("snapshot debugger reset.");
        }

        public static void SaveCustomUndoData(string methodName, object data, object methodClassInstance)
        {
            var serializaeData = SerializationUtility.SerializeValue(data, DataFormat.JSON);
            if (_hybridUndoSlotCounter != 0)
            {
                NewMethodUndoRelatedList.Add(new UndoDataClass(methodName, MethodUndoTypes.Custom, _currentHybridUndoID,
                    (methodClassInstance, serializaeData), false));
                _hybridUndoSlotCounter--;
            }
            else
            {
                NewMethodUndoRelatedList.Add(new UndoDataClass(methodName, MethodUndoTypes.Custom, Guid.Empty,
                    (methodClassInstance, serializaeData), false));
            }
        }

        public static void SaveComponents(string methodName, List<Component> componentsToSave)
        {
            foreach (var component in componentsToSave)
            {
                CustomPlayModeSerializer.SaveComponent(component);
            }

            if (_hybridUndoSlotCounter != 0)
            {
                NewMethodUndoRelatedList.Add(new UndoDataClass(methodName, MethodUndoTypes.playmodeSave,
                    _currentHybridUndoID, componentsToSave, false));
                _hybridUndoSlotCounter--;
            }
            else
            {
                NewMethodUndoRelatedList.Add(new UndoDataClass(methodName, MethodUndoTypes.playmodeSave, Guid.Empty,
                    componentsToSave, false));
            }
        }

        public static void CreateHybridUndoSlot(int count)
        {
            _hybridUndoSlotCounter = count;
            _currentHybridUndoID = Guid.NewGuid();
        }
    }

    public class SnapShotDataStructure
    {
        public string MethodName { get; set; }
        public GameObject InstanceGameObject;
        public string InstanceInterfaceOrAbstrctClassName;
        public object MethodClassInstance { get; set; }
        public object ParametersData { get; set; }

        /// <summary>
        /// it can be serialization data if its unity object or it can be byte[] if its non-unity object
        /// </summary>
        public byte[] DataBytesArray { get; set; }

        public List<Object> ReferncedObjectsList { get; set; }
        public StackFrame[] StackFrameOfTheMethod { get; set; }
        public bool IsEventBasedExecution;
        public string DataJsonStringForReadability;

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
            return (value / Math.Pow(1024, (long) unit)).ToString("0.00");
        }
    }
}
#endif