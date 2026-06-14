using System;

namespace _.Scripts.Utility
{
    public class MODEL_IDENTITIES
    {
        public const string SESSION_MODEL = "SESSION_MODEL";
        
#if UNITY_EDITOR

        public static string[] VARIANTS = new string[]
        {
            SESSION_MODEL,
        };

#endif
    }
}