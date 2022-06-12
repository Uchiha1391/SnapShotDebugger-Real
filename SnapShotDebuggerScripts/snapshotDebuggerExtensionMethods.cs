using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NewGame;
using UnityEngine;

public static class snapshotDebuggerExtensionMethods
{
    public static T GetComponentSaved<T>(this GameObject gameObject,[CallerMemberName]string callername="") where T : Component
    {
        var tt = gameObject.GetComponent<T>();
        SnapshotDebubber.SaveComponent(callername,tt);

        return tt;
    }
}