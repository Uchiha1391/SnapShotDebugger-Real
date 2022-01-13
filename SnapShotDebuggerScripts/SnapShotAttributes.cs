using System;

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
}