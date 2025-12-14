using Haley.Abstractions;
using Haley.Enums;
using Haley.Internal;
using Haley.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Haley.Internal.QueryFields;

namespace Haley.Utils {
    internal static class InternalUtils {
        public static (int definitionVersion, string externalRef) ParseInstanceKey(this LifeCycleKey key, IAdapterGateway agw = null, string adapter_key = null) {
            int defVersion = 0;
            string externalRef = string.Empty;
            if (key.Type == LifeCycleKeyType.Parent) {
                //So, try to fetch the latest version.
                //keys[0] = definitionName:string
                //keys[1] = externalRef:string
                //keys[2] = environmentCode:int 
                var defName = ToObj<string>(key.keys[0]);
                var extRef = ToObj<string>(key.keys[1]);
                var envCode = ToObj<int>(key.keys[2]);

                var latestDef = agw?.ReadSingleAsync(adapter_key, QRY_DEF_VERSION.GET_LATEST_BY_ENV, (NAME, defName), (CODE, envCode)).Result;
                if (latestDef == null || !latestDef.Status || latestDef.Result == null || latestDef.Result.Count < 1) throw new ArgumentException($@"Err 01: Unable to fetch the latest version for the given definition {defName} and environmentCode {envCode}");
                if (!latestDef.Result.TryGetValue("id", out var defVersionObj) || defVersionObj == null || !int.TryParse(defVersionObj.ToString(), out defVersion)) throw new ArgumentException($@"Err 02: Unable to fetch the latest version ID for the given definition {defName} and environmentCode {envCode}");
            } else {
                defVersion = key.keys[0] is int dv ? dv : int.Parse(key.keys[0]?.ToString() ?? "0");
                externalRef = key.keys[1]?.ToString() ?? string.Empty;
            }
            return (defVersion, externalRef);
        }

        static T ToObj<T>(object? v) {
            if (v is null) throw new ArgumentNullException(nameof(v));
            if (v is T t) return t;
            return (T)Convert.ChangeType(v, typeof(T));
        }

    }
}
