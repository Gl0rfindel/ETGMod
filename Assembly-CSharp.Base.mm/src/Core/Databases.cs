using System;
using UnityEngine;
using System.Collections.Generic;

public static partial class ETGMod {

    /// <summary>
    /// ETGMod database configuration.
    /// </summary>
    public static class Databases {
        public static readonly ItemDB Items = new ItemDB();
        public static readonly StringDB Strings = new StringDB();
    }

}
