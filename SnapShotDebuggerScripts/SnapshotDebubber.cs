using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using ES3Internal;
using JetBrains.Annotations;
using MoreLinq;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace NewGame
{
    public class SnapShotAttributes
    {
        [AttributeUsage(AttributeTargets.Class)]
        public class SnapShotInjectionAttribute : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class IgnoreSnapShotInjectionAttribute : Attribute
        {
        }
    }


    public class SnapshotDebubber
    {
        private static int _numberOfSnapShotstaken;
        private static bool IsUndo;

        private static int
            CurrentUndoMethodIndex; // very very important. it tracks how many methods will get undo or redo. also its from reverse order . check where its set

        private static readonly List<(string, ES3SerializableSettings)> AutosaveKeysAndSettinglist =
            new List<(string, ES3SerializableSettings)>();

        [CanBeNull] private static readonly List<( string MethodName, object Instance, object[] parametersData,
            Dictionary<string, int>
            RereferenceIndexers, SerializationData OdinData, StackFrame[] StackFrameOfTheMethod)> MethodsRelatedData
            = new List<(string MethodName, object Instance, object[] parametersData, Dictionary<string, int>
                RereferenceIndexers, SerializationData OdinData, StackFrame[] StackFrameOfTheMethod)>();

        private static readonly List<object> ReferenceTypesStoreList = new List<object>();
        private static (ES3SerializableSettings NewSetting, string NewKey) CurrentStateGameobjectsSavekeyAndEs3Setting;

        /// <summary>
        ///     it stores all the serialization data that is created for to retrive current state
        /// </summary>
        private static List<(Object, SerializationData)> _currentStateOdinSerializedDataList;

        public static void TakeSnapshot(object MethodClassInstance, string MethodName,
            StackFrame[] StackFrameOfTheMethod)
        {
            if (IsUndo)
            {
               // EditorUtility.DisplayDialog("snapshot debugger", "undo is activated so no snapshot taken", "ok");

                return;
            }


            // not using es3 anymore
            //if (FileForMethodFieldsSetting == null)
            //{
            //    FileForMethodFieldsSetting =
            //        new ES3SerializableSettings("SnapshotDebugger-FileForMethodFieldsSetting--" +
            //                                    Guid.NewGuid().ToString());
            //}


            try
            {
                var OdinDataInstance = new SerializationData();

                if (MethodClassInstance != null)
                {
                    UnitySerializationUtility.SerializeUnityObject((Object) MethodClassInstance, ref OdinDataInstance,
                        true,
                        new SerializationContext
                        {
                            Config = new SerializationConfig {SerializationPolicy = SerializationPolicies.Everything}
                        });
                    var checkJson = Encoding.ASCII.GetString(OdinDataInstance.SerializedBytes);
                }

                #region handle references

                var ReferenceDictonary = new Dictionary<string, int>();
                var Type = MethodClassInstance.GetType();
                var FieldInfos = Type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

                //
                foreach (var FieldInfo in FieldInfos)
                {
                    var MemberInfoType = FieldInfo.FieldType;
                    if (MemberInfoType != typeof(string)
                    ) // string is a special case. maybe there will be more immutable refernece type that can cause problems tag:#RiskyCode
                        if (!MemberInfoType.IsValueType)
                        {
                            ReferenceTypesStoreList.Add(FieldInfo.GetValue(MethodClassInstance));
                            ReferenceDictonary.Add(FieldInfo.Name, ReferenceTypesStoreList.Count);
                        }
                }

                #endregion


                MethodsRelatedData.Add((MethodName, MethodClassInstance, null,
                    ReferenceDictonary, OdinDataInstance, StackFrameOfTheMethod));

                #region saving gameobjects

                var ( newSetting, newKey) = NewKeyAndEs3SettingGenerator();
                AutosaveKeysAndSettinglist.Add((newKey, newSetting));
                ES3AutoSaveMgr._current.Save(newKey, newSetting);

                #endregion


                ConsoleProDebug.Watch("No. of snapshots taken", _numberOfSnapShotstaken.ToString());
                _numberOfSnapShotstaken++;
            }
            catch (Exception e)
            {
                // Get stack trace for the exception with source file information
                var st = new StackTrace(e, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();

                Debug.LogError(e + "-" + line);

                throw;
            }
        }

        private static ( ES3SerializableSettings newSetting, string newKey) NewKeyAndEs3SettingGenerator()
        {
            var newpath = "es3saver" + Guid.NewGuid() + ".es3";
            var newSetting = new ES3SerializableSettings(newpath);
            var newKey = Guid.NewGuid().ToString();
            return (newSetting, newKey);
        }


        public static void TakeSnapshotForParameters(object[] parameters)
        {
            try
            {
                if (IsUndo) return;

                if (parameters.Length <= 0) return;
                var ValueTuple =
                    MethodsRelatedData.Last();


                var
                    ParametersWithUnityObjectsReferences =
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

                ValueTuple.parametersData = ParametersWithUnityObjectsReferences.ToArray();

                MethodsRelatedData.RemoveAt(MethodsRelatedData.Count - 1);
                MethodsRelatedData.Add(ValueTuple);
            }
            catch (Exception e)
            {
                Debug.LogError(e);

                throw;
            }
        }

        public static void UndloadSnapShot(int indexOfMethod)
        {
            if (indexOfMethod > MethodsRelatedData.Count)
            {
                Debug.LogWarning("the index is bigger than the count of methods list");
                return;
            }

            IsUndo = true;

            #region saving state for to retrieve current state

            #region save gameobjects current state

            var (NewSetting, NewKey) = NewKeyAndEs3SettingGenerator();
            CurrentStateGameobjectsSavekeyAndEs3Setting = (NewSetting, NewKey);
            ES3AutoSaveMgr._current.Save(NewKey, NewSetting);

            #endregion


            CurrentStateOdinSerialize();

            #endregion

            CurrentUndoMethodIndex = MethodsRelatedData.Count - indexOfMethod;

            for (var i = 0; i < CurrentUndoMethodIndex; i++)
            {
                #region loading gameobjects original values

                if (AutosaveKeysAndSettinglist.Count > 0)
                {
                    var (key, Es3SerializableSettings) =
                        AutosaveKeysAndSettinglist[AutosaveKeysAndSettinglist.Count - i - 1];
                    ES3AutoSaveMgr._current.Load(key, Es3SerializableSettings);
                }

                #endregion

                #region method class fields data restore

                if (MethodsRelatedData.Count > 0)
                {
                    var (MethodName, Instance, ParametersData, RereferenceIndexers, OdinData, StackFrameOfTheMethod) =
                        MethodsRelatedData[MethodsRelatedData.Count - i - 1];


                    if (Instance != null)
                    {
                        var checkJson = Encoding.ASCII.GetString(OdinData.SerializedBytes);
                        //UnitySerializationUtility.DeserializeUnityObject((Object) Instance, ref OdinData);
                        UnitySerializationUtility.DeserializeUnityObject((Object) Instance, ref OdinData,
                            new DeserializationContext
                            {
                                Config = new SerializationConfig
                                    {SerializationPolicy = SerializationPolicies.Everything}
                            });
                    }


                    #region invoke the indexed method

                    if (i == CurrentUndoMethodIndex - 1)
                    {
                        var Type = Instance.GetType();
                        var MethodInfo = Type.GetMethod(MethodName,
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);


                        var FixedParameterdata = new List<object>();

                        #region fixing parameters and fetching gameobjects references from es3 references

                        if (ParametersData != null)
                            foreach (var CurrentData in ParametersData)
                                if (CurrentData is long) // I never use long so its ok. tag:#RiskyCode
                                {
                                    var gameobject = ES3ReferenceMgrBase.Current.Get((long) CurrentData);
                                    FixedParameterdata.Add(gameobject);
                                }
                                else
                                {
                                    FixedParameterdata.Add(CurrentData);
                                }

                        #endregion

                        MethodInfo?.Invoke(Instance, FixedParameterdata.ToArray());
                        //GetToCurrentState();
                    }

                    #endregion
                }

                #endregion
            }

            EditorUtility.DisplayDialog("snapshot debugger", "undo is activated so no snapshot taken", "ok");

        }

        /// <summary>
        /// /
        /// </summary>
        public void OnEntry()
        {
            TakeSnapshot(this, MethodBase.GetCurrentMethod().Name, new StackTrace().GetFrames());
        }

        public static void GetToCurrentState()
        {
            if (CurrentUndoMethodIndex == 0)
            {
                EditorUtility.DisplayDialog("snapshot debugger", "Current is already Active dumbass", "ok");
                return;
            }

            CurrentStateOdinDeSerialize(); // this needed to be executed before refernces are fixed


            #region fixed duplicate methods executions, new code

            var distincts = MethodsRelatedData.DistinctBy(tuple => tuple.Instance).ToList();

            for (var i = 0; i < distincts.Count; i++)
            {
                var (MethodName, Instance, ParametersData, RereferenceIndexers, OdinData, StackFrameOfTheMethod) =
                    distincts[i];

                #region fixing refrences

                foreach (var FieldInfo in Instance.GetType()
                    .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
                    if (RereferenceIndexers.ContainsKey(FieldInfo.Name))
                    {
                        var RereferenceIndexer = RereferenceIndexers[FieldInfo.Name];
                        var data = ReferenceTypesStoreList[RereferenceIndexer - 1];

                        FieldInfo.SetValue(Instance, data);
                    }

                #endregion
            }

            #endregion


            #region old code

            //for (int i = 0; i <MethodsRelatedData.Count-CurrentUndoMethodIndex; i++)
            //{
            //    var (MethodName, Instance, ParametersData, RereferenceIndexers, OdinData) = MethodsRelatedData[CurrentUndoMethodIndex + i ];

            //    #region fixing refrences

            //    foreach (var FieldInfo in Instance.GetType()
            //        .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
            //    {
            //        if (RereferenceIndexers.ContainsKey(FieldInfo.Name))
            //        {
            //            var RereferenceIndexer = RereferenceIndexers[FieldInfo.Name];
            //            var data = ReferenceTypesStoreList[RereferenceIndexer - 1];

            //            FieldInfo.SetValue(Instance, data);
            //        }
            //    }

            //    #endregion

            //}

            #endregion

            CurrentUndoMethodIndex = 0;


            var (newSetting, key) = CurrentStateGameobjectsSavekeyAndEs3Setting;
            ES3AutoSaveMgr._current.Load(key, newSetting);

            IsUndo = false;
            EditorUtility.DisplayDialog("snapshot debugger", "Current is Active now", "ok");
        }

        private static void CurrentStateOdinSerialize()
        {
            _currentStateOdinSerializedDataList = new List<(Object, SerializationData)>();
            var Distincts = MethodsRelatedData.DistinctBy(tuple => tuple.Instance).ToList();

            foreach (var ValueTuple in Distincts)
            {
                var InstanceBaseType = ValueTuple.Instance.GetType().BaseType;
                if (InstanceBaseType == typeof(MonoBehaviour))
                {
                    var data = new SerializationData();

                    UnitySerializationUtility.SerializeUnityObject((Object) ValueTuple.Instance, ref data, true,
                        new SerializationContext
                        {
                            Config = new SerializationConfig {SerializationPolicy = SerializationPolicies.Everything}
                        });


                    _currentStateOdinSerializedDataList.Add(((Object) ValueTuple.Instance, data));
                }
            }
        }


        private static void CurrentStateOdinDeSerialize()
        {
            foreach (var ValueTuple in _currentStateOdinSerializedDataList)
            {
                var Tuple = ValueTuple;
                var checkJson = Encoding.ASCII.GetString(Tuple.Item2.SerializedBytes);

                UnitySerializationUtility.DeserializeUnityObject(Tuple.Item1, ref Tuple.Item2,
                    new DeserializationContext
                        {Config = new SerializationConfig {SerializationPolicy = SerializationPolicies.Everything}});
            }
        }

        public static void LogMethodds()
        {
            for (var Index = 0; Index < MethodsRelatedData.Count; Index++)
            {
                var ValueTuple =
                    MethodsRelatedData[Index];
                ConsoleProDebug.LogToFilter(ValueTuple.Instance + ":" + ValueTuple.MethodName + "--Index=" + Index,
                    "SnapShotMethods");
            }
        }

        public static void LogStackFrameOfGivenMethod(int IndexOFMethod)
        {
            var ValueTuple =
                MethodsRelatedData[IndexOFMethod];

            foreach (var StackFrame in ValueTuple.StackFrameOfTheMethod)
                ConsoleProDebug.LogToFilter(StackFrame.ToString(),
                    "StackframeOfGivenMethod");
        }

        /// <summary>
        /// actually its just for checking size. now i Now its too small so I shouldnt care
        /// </summary>
        public static void SizeofDebuggingData()
        {
            var dd = SerializationUtility.SerializeValue(MethodsRelatedData, DataFormat.JSON);
            var json = Encoding.ASCII.GetString(dd);

            var size = SizeConverterCustom.ToSize(dd.Length * sizeof(byte), SizeConverterCustom.SizeUnits.MB);
            EditorUtility.DisplayDialog("Size of debuuger list", size, "ok");
        }

        public static void OverWriteES3autoSaveOfCurrentExecutingMethod()
        {
            var (NewSetting, NewKey) = NewKeyAndEs3SettingGenerator();

            AutosaveKeysAndSettinglist[AutosaveKeysAndSettinglist.Count - 1] = (NewKey, NewSetting);
            ES3AutoSaveMgr._current.Save(NewKey, NewSetting);

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

        public static string ToSize(this Int64 value, SizeUnits unit)
        {
            return (value / (double) Math.Pow(1024, (Int64) unit)).ToString("0.00");
        }
    }
}