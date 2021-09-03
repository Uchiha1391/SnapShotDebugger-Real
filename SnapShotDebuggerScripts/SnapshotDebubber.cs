using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using ES3Internal;
using Sirenix.Serialization;
using UnityEditor;
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
        public static int NumberOfSnapShotstaken { get; private set; }

        public static int CurrentUndoMethodIndex { get; private set; }

        public static List<(string, ES3SerializableSettings)>
            AutosaveKeysAndSettinglist { get; } =
            new List<(string, ES3SerializableSettings)>();

        public static List<SnapShotDataStructure> MethodsRelatedData { get;
            private set;
        } =
            new List<SnapShotDataStructure>();


      

        public static void TakeSnapshot(object MethodClassInstance,
            string MethodName,
            StackFrame[] StackFrameOfTheMethod)
        {


            try
            {
                var OdinDataInstance = new SerializationData();

                if (MethodClassInstance != null)
                {
                    UnitySerializationUtility.SerializeUnityObject(
                        (Object) MethodClassInstance, ref OdinDataInstance,
                        true,
                        new SerializationContext
                        {
                            Config = new SerializationConfig
                            {
                                SerializationPolicy =
                                    SerializationPolicies.Everything
                            }
                        });
                    var checkJson =
                        Encoding.ASCII.GetString(OdinDataInstance
                            .SerializedBytes);
                }


                MethodsRelatedData.Add(new SnapShotDataStructure(MethodName,
                    MethodClassInstance, null,
                    OdinDataInstance, StackFrameOfTheMethod));

                #region saving gameobjects

                var (newSetting, newKey) = NewKeyAndEs3SettingGenerator();
                AutosaveKeysAndSettinglist.Add((newKey, newSetting));
                ES3AutoSaveMgr._current.Save(newKey, newSetting);

                #endregion


                ConsoleProDebug.Watch("No. of snapshots taken",
                    NumberOfSnapShotstaken.ToString());
                NumberOfSnapShotstaken++;
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

        private static ( ES3SerializableSettings newSetting, string newKey)
            NewKeyAndEs3SettingGenerator()
        {
            var newpath = "es3saver" + Guid.NewGuid() + ".es3";
            var newSetting = new ES3SerializableSettings(newpath);
            var newKey = Guid.NewGuid().ToString();
            return (newSetting, newKey);
        }


        public static void UndloadSnapShot(int indexOfMethod)
        {
            if (indexOfMethod > MethodsRelatedData.Count)
            {
                Debug.LogWarning(
                    "the index is bigger than the count of methods list");
                return;
            }



            CurrentUndoMethodIndex = MethodsRelatedData.Count - indexOfMethod;

            for (var i = 0; i < CurrentUndoMethodIndex; i++)
            {
                #region loading gameobjects original values

                if (AutosaveKeysAndSettinglist.Count > 0)
                {
                    var (key, Es3SerializableSettings) =
                        AutosaveKeysAndSettinglist[
                            AutosaveKeysAndSettinglist.Count - i - 1];
                    ES3AutoSaveMgr._current.Load(key, Es3SerializableSettings);
                }

                #endregion

                #region method class fields data restore

                if (MethodsRelatedData.Count > 0)
                {
                    var MethoDataStructure =
                        MethodsRelatedData[MethodsRelatedData.Count - i - 1];


                    if (MethoDataStructure.MethodClassInstance != null)
                    {
                        var OdinDataInstance =
                            MethoDataStructure.OdinDataInstance;
                        var checkJson =
                            Encoding.ASCII.GetString(OdinDataInstance
                                .SerializedBytes);
                        //UnitySerializationUtility.DeserializeUnityObject((Object) Instance, ref OdinData);
                        UnitySerializationUtility.DeserializeUnityObject(
                            (Object) MethoDataStructure.MethodClassInstance,
                            ref OdinDataInstance,
                            new DeserializationContext
                            {
                                Config = new SerializationConfig
                                {
                                    SerializationPolicy = SerializationPolicies
                                        .Everything
                                }
                            });
                    }


                    #region invoke the indexed method

                    //if (i == CurrentUndoMethodIndex - 1)
                    //{
                    //    var Type = MethoDataStructure.MethodClassInstance.GetType();
                    //    var MethodInfo = Type.GetMethod(MethodName,
                    //        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);


                    //    var FixedParameterdata = new List<object>();

                    //    #region fixing parameters and fetching gameobjects references from es3 references

                    //    if (ParametersData != null)
                    //        foreach (var CurrentData in ParametersData)
                    //            if (CurrentData is long) // I never use long so its ok. tag:#RiskyCode
                    //            {
                    //                var gameobject = ES3ReferenceMgrBase.Current.Get((long) CurrentData);
                    //                FixedParameterdata.Add(gameobject);
                    //            }
                    //            else
                    //            {
                    //                FixedParameterdata.Add(CurrentData);
                    //            }

                    //    #endregion

                    //    MethodInfo?.Invoke(Instance, FixedParameterdata.ToArray());
                    //    //GetToCurrentState();
                    //}

                    #endregion
                }

                #endregion
            }

            EditorUtility.DisplayDialog("snapshot debugger",
                "undo is activated so no snapshot taken", "ok");
        }


        #region they are used for Il injectoin

        public void OnEntry()
        {
            TakeSnapshot(this, MethodBase.GetCurrentMethod().Name,
                new StackTrace().GetFrames());
        }

        public static void TakeSnapshotForParameters(object[] parameters)
        {
            try
            {

                if (parameters.Length <= 0)
                    return;
                var ValueTuple =
                    MethodsRelatedData.Last();


                var
                    ParametersWithUnityObjectsReferences =
                        new List<
                            object>(); //if gameobject gets destroyed then es3 refrence will help as es3 creates a new gameobject if existing is not present.
                foreach (var Parameter in parameters)
                    if (Parameter != null &&
                        Parameter.GetType() == typeof(Object))
                    {
                        var reference =
                            ES3ReferenceMgrBase.Current.Get((Object) Parameter);
                        ParametersWithUnityObjectsReferences.Add(reference);
                    }
                    else
                    {
                        ParametersWithUnityObjectsReferences.Add(Parameter);
                    }

                ValueTuple.ParametersData =
                    ParametersWithUnityObjectsReferences.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogError(e);

                throw;
            }
        }

        #endregion


        public static void LogMethodds()
        {
            for (var Index = 0; Index < MethodsRelatedData.Count; Index++)
            {
                var ValueTuple =
                    MethodsRelatedData[Index];
                ConsoleProDebug.LogToFilter(
                    ValueTuple.MethodClassInstance + ":" +
                    ValueTuple.MethodName + "--Index=" + Index,
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
        ///     actually its just for checking size. now i Now its too small so I shouldnt care
        /// </summary>
        public static void SizeofDebuggingData()
        {
            var dd = SerializationUtility.SerializeValue(MethodsRelatedData,
                DataFormat.JSON);
            var json = Encoding.ASCII.GetString(dd);

            var size = SizeConverterCustom.ToSize(dd.Length * sizeof(byte),
                SizeConverterCustom.SizeUnits.MB);
            EditorUtility.DisplayDialog("Size of debuuger list", size, "ok");
        }
    }

    public class SnapShotDataStructure
    {
        public string MethodName { get; set; }
        public object MethodClassInstance { get; set; }
        public object ParametersData { get; set; }
        public SerializationData OdinDataInstance { get; set; }
        public StackFrame[] StackFrameOfTheMethod { get; set; }

        public SnapShotDataStructure(string methodName,
            object methodClassInstance,
            object parametersData,
            SerializationData odinDataInstance,
            StackFrame[] stackFrameOfTheMethod)
        {
            MethodName = methodName;
            MethodClassInstance = methodClassInstance;
            ParametersData = parametersData;
            OdinDataInstance = odinDataInstance;
            StackFrameOfTheMethod = stackFrameOfTheMethod;
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